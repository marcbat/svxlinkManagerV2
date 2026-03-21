using LanguageExt;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Salons.DeactivateSalon;

/// <summary>
/// Commande pour désactiver un Salon (déconnexion du reflector).
/// </summary>
/// <param name="Id">Identifiant unique du salon à désactiver</param>
public record DeactivateSalonCommand(Guid Id);

/// <summary>
/// Handler pour la commande DeactivateSalonCommand
/// </summary>
public static class DeactivateSalonCommandHandler
{
    /// <summary>
    /// Désactive le Salon : arrête le daemon SVXLink, vide les nœuds connectés
    /// et met à jour le tracker d'état runtime.
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        DeactivateSalonCommand command,
        IActiveSessionTracker tracker,
        ISvxLinkDaemonService daemonService,
        IConnectedNodesService connectedNodesService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Désactivation du Salon {SalonId}", command.Id);

        if (!tracker.IsSalonActive(command.Id))
            return Error.Validation("SALON_NOT_ACTIVE", "Ce salon n'est pas actuellement actif").ToFailure<Unit>();

        var stopResult = await daemonService.StopAsync(cancellationToken);
        if (stopResult.IsFail)
            return Error.Validation("SVXLINK_STOP_ERROR", "Impossible d'arrêter le daemon SVXLink").ToFailure<Unit>();

        connectedNodesService.Reset();
        tracker.SetActiveSalon(null);

        logger.LogInformation("Salon {SalonId} désactivé avec succès", command.Id);
        return unit.ToSuccess();
    }
}
