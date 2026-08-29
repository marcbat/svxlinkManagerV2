using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service hébergé qui traite les commandes DTMF d'annonce vocale (plage 300-399).
/// L'audio est déclenché côté SVXLink par Logic.tcl ; ce service orchestre aussi
/// la synthèse vocale TTS pour les commandes d'information (301-398).
///
/// Commandes supportées :
///   300 — Annonce contextuelle du salon actif (TTS dynamique : callsign, nom, fréquence TX si split)
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

    /// <summary>Borne inférieure de la plage des commandes d'annonce (synchronisée avec DtmfCodeRanges).</summary>
    internal const int RangeMin = DtmfCodeRanges.AnnounceRangeMin;

    /// <summary>Borne supérieure de la plage des commandes d'annonce (synchronisée avec DtmfCodeRanges).</summary>
    internal const int RangeMax = DtmfCodeRanges.AnnounceRangeMax;

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

            // Les commandes système (310-320) sont traitées par DtmfSystemCommandService
            if (DtmfSystemCommands.IsSystemCommand(dtmfCode))
            {
                _logger.LogDebug(
                    "Commande DTMF {DtmfCode} (système) ignorée dans DtmfAnnounceService", dtmfCode);
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

        _logger.LogInformation("Commande DTMF 300 : annonce contextuelle du salon actif « {SalonName} »", salon.Name);

        await _semaphore.WaitAsync();
        try
        {
            var text = BuildAnnounceText(salon);
            var ttsResult = await _ttsService.GenerateWavAsync(text, TtsWavPath);
            if (ttsResult.IsFail)
            {
                _logger.LogWarning("Échec de la synthèse TTS pour la commande DTMF 300");
                return;
            }

            var ptyResult = await _ptyWriter.SendCommandAsync(TtsInternalCode.ToString());
            if (ptyResult.IsFail)
                _logger.LogWarning("Échec de l'envoi de la commande PTY pour la commande DTMF 300");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    internal static string BuildAnnounceText(SalonAggregate salon)
    {
        var config = salon.Configuration;
        var text = $"Vous êtes sur le link {config.SimplexCallsign} actuellement connecté sur {salon.Name}.";

        if (config.RxFrequency != config.TxFrequency)
            text += $" La fréquence d'émission est {FormatFrequency(config.TxFrequency)}.";

        return text;
    }

    internal static string FormatFrequency(decimal frequency)
    {
        var intPart = (int)frequency;
        var decPart = (int)Math.Round((frequency - intPart) * 1000);
        return $"{intPart} virgule {decPart:D3} mégahertz";
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
