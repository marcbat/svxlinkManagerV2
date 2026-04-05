using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;
using SvxlinkManagerV2.Application.Features.Salons.GetSalonByDtmfCode;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service hébergé qui écoute les commandes DTMF et déclenche le changement de salon.
/// Utilise IServiceScopeFactory pour créer un scope MediatR à chaque commande DTMF.
/// </summary>
public class DtmfSalonSwitchService : IHostedService
{
    private readonly IDtmfCommandTracker _dtmfTracker;
    private readonly IActiveSessionTracker _sessionTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DtmfSalonSwitchService> _logger;

    public DtmfSalonSwitchService(
        IDtmfCommandTracker dtmfTracker,
        IActiveSessionTracker sessionTracker,
        IServiceScopeFactory scopeFactory,
        ILogger<DtmfSalonSwitchService> logger)
    {
        _dtmfTracker = dtmfTracker;
        _sessionTracker = sessionTracker;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _dtmfTracker.OnDtmfCommandReceived += OnDtmfCommandReceived;
        _logger.LogInformation("DtmfSalonSwitchService démarré et abonné aux commandes DTMF");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _dtmfTracker.OnDtmfCommandReceived -= OnDtmfCommandReceived;
        _logger.LogInformation("DtmfSalonSwitchService arrêté");
        return Task.CompletedTask;
    }

    private async void OnDtmfCommandReceived(string rawCommand)
    {
        try
        {
            _logger.LogInformation("Commande DTMF reçue : {RawCommand}", rawCommand);

            // Parser le code DTMF
            if (!int.TryParse(rawCommand.Trim(), out var dtmfCode) || dtmfCode < 1 || dtmfCode > 9999)
            {
                _logger.LogWarning("Code DTMF invalide ignoré : {RawCommand}", rawCommand);
                return;
            }

            // La plage 300-399 est réservée aux commandes d'annonce (info commands),
            // traitées par DtmfAnnounceService — ignorer silencieusement ici.
            if (dtmfCode >= 300 && dtmfCode <= 399)
                return;

            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Rechercher le salon correspondant au code DTMF
            var salon = await mediator.Send(new GetSalonByDtmfCodeQuery(dtmfCode));
            if (salon == null)
            {
                _logger.LogWarning("Aucun salon configuré pour le code DTMF {DtmfCode}", dtmfCode);
                return;
            }

            // Vérifier si le salon est déjà actif
            if (_sessionTracker.IsSalonActive(salon.Id))
            {
                _logger.LogInformation("Le salon {SalonName} est déjà actif, commande DTMF {DtmfCode} ignorée",
                    salon.Name, dtmfCode);
                return;
            }

            // Activer le salon
            _logger.LogInformation("Activation du salon {SalonName} via commande DTMF {DtmfCode}",
                salon.Name, dtmfCode);

            var result = await mediator.Send(new ActivateSalonCommand(salon.Id));
            result.Match(
                Succ: _ =>
                {
                    _logger.LogInformation("Salon {SalonName} activé avec succès via DTMF {DtmfCode}",
                        salon.Name, dtmfCode);
                    return LanguageExt.Unit.Default;
                },
                Fail: errors =>
                {
                    _logger.LogError("Échec de l'activation du salon {SalonName} via DTMF : {Errors}",
                        salon.Name, string.Join(", ", errors));
                    return LanguageExt.Unit.Default;
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du traitement de la commande DTMF : {RawCommand}", rawCommand);
        }
    }
}
