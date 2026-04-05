using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service hébergé qui traite les commandes DTMF d'annonce vocale (plage 300-399).
/// L'audio est déclenché côté SVXLink par Logic.tcl ; ce service assure le logging
/// et constitue le point d'extension pour les commandes d'information futures.
///
/// Commandes supportées :
///   300 — Rejoue le nom du salon actif (Name.wav, déployé par ActivateSalonCommand)
/// </summary>
public class DtmfAnnounceService : IHostedService
{
    private readonly IDtmfCommandTracker _dtmfTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DtmfAnnounceService> _logger;

    /// <summary>Borne inférieure de la plage des commandes d'annonce.</summary>
    internal const int RangeMin = 300;

    /// <summary>Borne supérieure de la plage des commandes d'annonce.</summary>
    internal const int RangeMax = 399;

    public DtmfAnnounceService(
        IDtmfCommandTracker dtmfTracker,
        IServiceScopeFactory scopeFactory,
        ILogger<DtmfAnnounceService> logger)
    {
        _dtmfTracker = dtmfTracker;
        _scopeFactory = scopeFactory;
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

            _logger.LogInformation("Commande d'annonce DTMF reçue : {DtmfCode}", dtmfCode);

            switch (dtmfCode)
            {
                case 300:
                    await HandleAnnounceActiveSalonAsync();
                    break;

                default:
                    _logger.LogDebug("Commande d'annonce DTMF {DtmfCode} non mappée, ignorée", dtmfCode);
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
}
