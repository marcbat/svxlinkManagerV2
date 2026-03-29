using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Salons.DeactivateSalon;

/// <summary>
/// Commande pour désactiver un Salon (déconnexion du reflector).
/// </summary>
/// <param name="Id">Identifiant unique du salon à désactiver</param>
public record DeactivateSalonCommand(Guid Id) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande DeactivateSalonCommand
/// </summary>
public class DeactivateSalonCommandHandler : IRequestHandler<DeactivateSalonCommand, Validation<Error, Unit>>
{
    private readonly IActiveSessionTracker _tracker;
    private readonly ISvxLinkDaemonService _daemonService;
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly ILogger<DeactivateSalonCommandHandler> _logger;

    public DeactivateSalonCommandHandler(
        IActiveSessionTracker tracker,
        ISvxLinkDaemonService daemonService,
        IConnectedNodesService connectedNodesService,
        ILogger<DeactivateSalonCommandHandler> logger)
    {
        _tracker = tracker;
        _daemonService = daemonService;
        _connectedNodesService = connectedNodesService;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        DeactivateSalonCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Désactivation du Salon {SalonId}", command.Id);

        if (!_tracker.IsSalonActive(command.Id))
            return Error.Validation("SALON_NOT_ACTIVE", "Ce salon n'est pas actuellement actif").ToFailure<Unit>();

        var stopResult = await _daemonService.StopAsync(cancellationToken);
        if (stopResult.IsFail)
            return Error.Validation("SVXLINK_STOP_ERROR", "Impossible d'arrêter le daemon SVXLink").ToFailure<Unit>();

        _connectedNodesService.Reset();
        _tracker.SetActiveSalon(null);

        _logger.LogInformation("Salon {SalonId} désactivé avec succès", command.Id);
        return unit.ToSuccess();
    }
}
