using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;
using SvxlinkManagerV2.Application.Features.Salons.ActivateStandaloneMode;
using SvxlinkManagerV2.Application.Features.Salons.GetAdjacentSalon;
using SvxlinkManagerV2.Application.Features.Salons.GetDefaultSalon;
using SvxlinkManagerV2.Application.Features.SvxLink.RestartSvxLink;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service hébergé qui traite les commandes DTMF système de la plage réservée 300-399
/// (voir <see cref="DtmfSystemCommands"/>) : pilotage du nœud par radio, sans interface web.
///
/// Commandes supportées :
///   310 — Retour au salon par défaut
///   311 — Déconnexion du salon actif (bascule en mode autonome)
///   312 — Salon suivant, par ordre de code DTMF
///   313 — Salon précédent
///   320 — Redémarrage du daemon SVXLink en conservant le salon actif
///
/// Chaque commande est confirmée vocalement. Les commandes qui redémarrent le daemon
/// annoncent APRÈS l'action : une annonce émise avant serait coupée par l'arrêt de SVXLink.
/// </summary>
public class DtmfSystemCommandService : IHostedService
{
    private readonly IDtmfCommandTracker _dtmfTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVoiceAnnouncementService _announcer;
    private readonly IActiveSessionTracker _sessionTracker;
    private readonly ILogger<DtmfSystemCommandService> _logger;

    public DtmfSystemCommandService(
        IDtmfCommandTracker dtmfTracker,
        IServiceScopeFactory scopeFactory,
        IVoiceAnnouncementService announcer,
        IActiveSessionTracker sessionTracker,
        ILogger<DtmfSystemCommandService> logger)
    {
        _dtmfTracker = dtmfTracker;
        _scopeFactory = scopeFactory;
        _announcer = announcer;
        _sessionTracker = sessionTracker;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _dtmfTracker.OnDtmfCommandReceived += OnDtmfCommandReceived;
        _logger.LogInformation("DtmfSystemCommandService démarré et abonné aux commandes DTMF");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _dtmfTracker.OnDtmfCommandReceived -= OnDtmfCommandReceived;
        _logger.LogInformation("DtmfSystemCommandService arrêté");
        return Task.CompletedTask;
    }

    private async void OnDtmfCommandReceived(string rawCommand)
    {
        try
        {
            if (!int.TryParse(rawCommand.Trim(), out var dtmfCode))
                return;

            if (!DtmfSystemCommands.IsSystemCommand(dtmfCode))
                return;

            _logger.LogInformation("Commande DTMF système reçue : {DtmfCode}", dtmfCode);

            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            switch (dtmfCode)
            {
                case DtmfSystemCommands.DefaultSalon:
                    await HandleDefaultSalonAsync(mediator);
                    break;

                case DtmfSystemCommands.Disconnect:
                    await HandleDisconnectAsync(mediator);
                    break;

                case DtmfSystemCommands.NextSalon:
                    await HandleNavigationAsync(mediator, SalonNavigationDirection.Next);
                    break;

                case DtmfSystemCommands.PreviousSalon:
                    await HandleNavigationAsync(mediator, SalonNavigationDirection.Previous);
                    break;

                case DtmfSystemCommands.RestartDaemon:
                    await HandleRestartAsync(mediator);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du traitement de la commande DTMF système : {RawCommand}", rawCommand);
        }
    }

    /// <summary>
    /// Commande 310 — retour au salon par défaut.
    /// Sans effet (mais annoncé) si ce salon est déjà actif ou si aucun n'est configuré.
    /// </summary>
    private async Task HandleDefaultSalonAsync(IMediator mediator)
    {
        var defaultSalon = await mediator.Send(new GetDefaultSalonQuery());

        if (defaultSalon is null)
        {
            _logger.LogInformation("Commande DTMF 310 : aucun salon par défaut configuré");
            await _announcer.AnnounceAsync("Aucun salon par défaut n'est configuré.");
            return;
        }

        if (_sessionTracker.IsSalonActive(defaultSalon.Id))
        {
            _logger.LogInformation(
                "Commande DTMF 310 : le salon par défaut « {SalonName} » est déjà actif", defaultSalon.Name);
            await _announcer.AnnounceAsync($"Le salon par défaut {defaultSalon.Name} est déjà actif.");
            return;
        }

        await ActivateAndAnnounceAsync(mediator, defaultSalon, $"Retour au salon par défaut {defaultSalon.Name}.");
    }

    /// <summary>
    /// Commande 311 — déconnexion du salon actif : le nœud bascule en mode autonome
    /// et reste à l'écoute des commandes DTMF.
    /// </summary>
    private async Task HandleDisconnectAsync(IMediator mediator)
    {
        if (!_sessionTracker.ActiveSalonId.HasValue)
        {
            _logger.LogInformation("Commande DTMF 311 : aucun salon actif, le nœud est déjà en mode autonome");
            await _announcer.AnnounceAsync("Aucun salon actif. Le nœud est déjà en mode autonome.");
            return;
        }

        var result = await mediator.Send(new ActivateStandaloneModeCommand(SalonActivationOrigin.SystemCommand));

        if (result.IsFail)
        {
            _logger.LogError("Commande DTMF 311 : échec de l'activation du mode autonome");
            await _announcer.AnnounceAsync("Échec de la déconnexion du salon.");
            return;
        }

        _logger.LogInformation("Commande DTMF 311 : mode autonome activé");
        await _announcer.AnnounceAsync("Salon déconnecté. Le nœud est en mode autonome.");
    }

    /// <summary>
    /// Commandes 312 et 313 — navigation en boucle dans la liste des salons dotés d'un code DTMF.
    /// </summary>
    private async Task HandleNavigationAsync(IMediator mediator, SalonNavigationDirection direction)
    {
        var salon = await mediator.Send(new GetAdjacentSalonQuery(direction));

        if (salon is null)
        {
            _logger.LogInformation("Navigation DTMF : aucun salon doté d'un code DTMF");
            await _announcer.AnnounceAsync("Aucun salon n'est configuré avec un code DTMF.");
            return;
        }

        // Liste réduite à un seul salon, déjà actif : la rotation retombe sur lui-même.
        if (_sessionTracker.IsSalonActive(salon.Id))
        {
            _logger.LogInformation(
                "Navigation DTMF : le salon « {SalonName} » est déjà actif, commande ignorée", salon.Name);
            await _announcer.AnnounceAsync($"Le salon {salon.Name} est déjà actif.");
            return;
        }

        await ActivateAndAnnounceAsync(mediator, salon, $"Connexion au salon {salon.Name}.");
    }

    /// <summary>
    /// Commande 320 — redémarrage du daemon SVXLink en conservant le salon actif.
    /// </summary>
    private async Task HandleRestartAsync(IMediator mediator)
    {
        var result = await mediator.Send(new RestartSvxLinkCommand());

        if (result.IsFail)
        {
            _logger.LogError("Commande DTMF 320 : échec du redémarrage du daemon SVXLink");
            await _announcer.AnnounceAsync("Échec du redémarrage de SVXLink.");
            return;
        }

        _logger.LogInformation("Commande DTMF 320 : daemon SVXLink redémarré");
        await _announcer.AnnounceAsync("SVXLink a redémarré.");
    }

    /// <summary>
    /// Active le salon puis confirme vocalement. L'annonce est émise après l'activation :
    /// le redémarrage du daemon couperait toute annonce en cours de lecture.
    /// </summary>
    private async Task ActivateAndAnnounceAsync(IMediator mediator, SalonAggregate salon, string successText)
    {
        _logger.LogInformation("Activation du salon « {SalonName} » via commande DTMF système", salon.Name);

        var result = await mediator.Send(new ActivateSalonCommand(salon.Id, SalonActivationOrigin.SystemCommand));

        if (result.IsFail)
        {
            _logger.LogError("Échec de l'activation du salon « {SalonName} » via commande DTMF système", salon.Name);
            await _announcer.AnnounceAsync($"Échec de la connexion au salon {salon.Name}.");
            return;
        }

        await _announcer.AnnounceAsync(successText);
    }
}
