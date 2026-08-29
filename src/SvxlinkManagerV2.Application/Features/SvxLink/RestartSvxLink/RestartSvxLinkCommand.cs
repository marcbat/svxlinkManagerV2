using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.SvxLink.RestartSvxLink;

/// <summary>
/// Commande de redémarrage du daemon SVXLink sans changer l'état de session.
/// Le salon actif est conservé : la configuration déjà générée est simplement rechargée.
/// </summary>
public record RestartSvxLinkCommand() : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande RestartSvxLinkCommand.
/// Sélectionne la version de SVXLink d'après le protocole du salon actif
/// (mode autonome : protocole V3, comme <c>ActivateStandaloneModeCommand</c>),
/// réarme l'annonce de connexion puis redémarre le daemon.
/// </summary>
public class RestartSvxLinkCommandHandler : IRequestHandler<RestartSvxLinkCommand, Validation<Error, Unit>>
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;
    private readonly ISvxLinkDaemonService _daemonService;
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly ILogger<RestartSvxLinkCommandHandler> _logger;

    public RestartSvxLinkCommandHandler(
        ISalonRepository repository,
        IActiveSessionTracker tracker,
        ISvxLinkDaemonService daemonService,
        IConnectedNodesService connectedNodesService,
        ILogger<RestartSvxLinkCommandHandler> logger)
    {
        _repository = repository;
        _tracker = tracker;
        _daemonService = daemonService;
        _connectedNodesService = connectedNodesService;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        RestartSvxLinkCommand command,
        CancellationToken cancellationToken)
    {
        var protocol = await ResolveProtocolAsync(cancellationToken);

        _logger.LogInformation("Redémarrage du daemon SVXLink (protocole: {Protocol})", protocol);

        // Réarme le service d'annonce de connexion pour la reconnexion à venir.
        _connectedNodesService.Reset();

        var daemonResult = await _daemonService.RestartAsync(protocol, cancellationToken);
        if (daemonResult.IsFail)
            return Error.Validation("SVXLINK_RESTART_ERROR", "Impossible de redémarrer le daemon SVXLink").ToFailure<Unit>();

        _logger.LogInformation("Daemon SVXLink redémarré avec succès");
        return unit.ToSuccess();
    }

    /// <summary>
    /// Détermine le protocole réflecteur à utiliser d'après le salon actif.
    /// </summary>
    private async Task<ReflectorProtocol> ResolveProtocolAsync(CancellationToken cancellationToken)
    {
        var activeSalonId = _tracker.ActiveSalonId;
        if (!activeSalonId.HasValue)
        {
            _logger.LogInformation("Aucun salon actif : redémarrage en mode autonome");
            return ReflectorProtocol.V3;
        }

        var aggregateResult = await _repository.GetByIdAsync(activeSalonId.Value, cancellationToken);

        return aggregateResult.Match(
            Succ: salon => salon.Configuration.ReflectorProtocol,
            Fail: _ =>
            {
                _logger.LogWarning(
                    "Salon actif {SalonId} introuvable : redémarrage avec le protocole V3 par défaut",
                    activeSalonId.Value);
                return ReflectorProtocol.V3;
            });
    }
}
