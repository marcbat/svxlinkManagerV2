using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service hébergé qui traite les commandes DTMF d'annonce vocale (plage 300-399).
/// L'audio est déclenché côté SVXLink par Logic.tcl ; ce service orchestre aussi
/// la synthèse vocale TTS pour les commandes d'information (301-398).
///
/// Commandes supportées :
///   300 — Rejoue le nom du salon actif (Name.wav, déployé par ActivateSalonCommand)
///   301–398 — Annonces vocales synthétisées via IInfoProvider + ITtsService + IDtmfPtyWriter
///   399 — Commande interne SVXLink (trigger lecture WAV TTS), jamais traitée ici
/// </summary>
public class DtmfAnnounceService : IHostedService
{
    private readonly IDtmfCommandTracker _dtmfTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnumerable<IInfoProvider> _infoProviders;
    private readonly ITtsService _ttsService;
    private readonly IDtmfPtyWriter _ptyWriter;
    private readonly ILogger<DtmfAnnounceService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>Borne inférieure de la plage des commandes d'annonce.</summary>
    internal const int RangeMin = 300;

    /// <summary>Borne supérieure de la plage des commandes d'annonce.</summary>
    internal const int RangeMax = 399;

    /// <summary>Code interne de déclenchement TTS côté SVXLink (jamais exposé aux opérateurs).</summary>
    internal const int TtsInternalCode = 399;

    /// <summary>Chemin du fichier WAV temporaire produit par le TTS.</summary>
    internal const string TtsWavPath = "/tmp/svxlink_tts.wav";

    public DtmfAnnounceService(
        IDtmfCommandTracker dtmfTracker,
        IServiceScopeFactory scopeFactory,
        IEnumerable<IInfoProvider> infoProviders,
        ITtsService ttsService,
        IDtmfPtyWriter ptyWriter,
        ILogger<DtmfAnnounceService> logger)
    {
        _dtmfTracker = dtmfTracker;
        _scopeFactory = scopeFactory;
        _infoProviders = infoProviders;
        _ttsService = ttsService;
        _ptyWriter = ptyWriter;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _dtmfTracker.OnDtmfCommandReceived += OnDtmfCommandReceived;
        _logger.LogInformation("DtmfAnnounceService démarré et abonné aux commandes DTMF");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _dtmfTracker.OnDtmfCommandReceived -= OnDtmfCommandReceived;
        _logger.LogInformation("DtmfAnnounceService arrêté");
        return Task.CompletedTask;
    }

    private async void OnDtmfCommandReceived(string rawCommand)
    {
        try
        {
            if (!int.TryParse(rawCommand.Trim(), out var dtmfCode))
                return;

            if (dtmfCode < RangeMin || dtmfCode > RangeMax)
                return;

            // La commande 399 est réservée à l'usage interne SVXLink — ne jamais la traiter
            if (dtmfCode == TtsInternalCode)
            {
                _logger.LogDebug("Commande DTMF 399 (interne) ignorée dans DtmfAnnounceService");
                return;
            }

            _logger.LogInformation("Commande d'annonce DTMF reçue : {DtmfCode}", dtmfCode);

            switch (dtmfCode)
            {
                case 300:
                    await HandleAnnounceActiveSalonAsync();
                    break;

                default:
                    await HandleInfoCommandAsync(dtmfCode);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du traitement de la commande d'annonce DTMF : {RawCommand}", rawCommand);
        }
    }

    private async Task HandleAnnounceActiveSalonAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var salon = await mediator.Send(new GetActiveSalonQuery());

        if (salon is null)
        {
            _logger.LogInformation("Commande DTMF 300 : aucun salon actif, annonce ignorée");
            return;
        }

        _logger.LogInformation("Commande DTMF 300 : annonce du salon actif « {SalonName} »", salon.Name);
    }

    private async Task HandleInfoCommandAsync(int dtmfCode)
    {
        var provider = _infoProviders.FirstOrDefault(p => p.DtmfCode == dtmfCode);
        if (provider is null)
        {
            _logger.LogDebug("Commande d'information DTMF {DtmfCode} sans provider, ignorée", dtmfCode);
            return;
        }

        _logger.LogInformation("Traitement de la commande d'information DTMF {DtmfCode} ({Description})",
            dtmfCode, provider.Description);

        await _semaphore.WaitAsync();
        try
        {
            var infoResult = await provider.GetInfoTextAsync();
            if (infoResult.IsFail)
            {
                _logger.LogWarning("Échec de la récupération d'information pour DTMF {DtmfCode}", dtmfCode);
                return;
            }

            var infoText = infoResult.Match(
                Succ: text => text,
                Fail: _ => string.Empty);

            var ttsResult = await _ttsService.GenerateWavAsync(infoText, TtsWavPath);
            if (ttsResult.IsFail)
            {
                _logger.LogWarning("Échec de la synthèse TTS pour DTMF {DtmfCode}", dtmfCode);
                return;
            }

            var ptyResult = await _ptyWriter.SendCommandAsync(TtsInternalCode.ToString());
            if (ptyResult.IsFail)
            {
                _logger.LogWarning("Échec de l'envoi de la commande PTY pour DTMF {DtmfCode}", dtmfCode);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
