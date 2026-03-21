using LanguageExt;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Reflectors.DeactivateReflector;

/// <summary>
/// Commande pour désactiver le Reflector (arrête le daemon svxreflector).
/// </summary>
/// <param name="Id">Identifiant unique du reflector à désactiver</param>
public record DeactivateReflectorCommand(Guid Id);

/// <summary>
/// Handler pour la commande DeactivateReflectorCommand.
/// Arrête le daemon svxreflector et met à jour le tracker d'état runtime.
/// </summary>
public static class DeactivateReflectorCommandHandler
{
    /// <summary>
    /// Désactive le Reflector : arrête le daemon svxreflector
    /// et met à jour le tracker d'état runtime.
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        DeactivateReflectorCommand command,
        IActiveSessionTracker tracker,
        IReflectorDaemonService daemonService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Désactivation du Reflector {ReflectorId}", command.Id);

        if (!tracker.IsReflectorActive(command.Id))
            return Error.Validation("REFLECTOR_NOT_ACTIVE", "Ce reflector n'est pas actuellement actif").ToFailure<Unit>();

        var result = await daemonService.StopAsync(cancellationToken);
        if (result.IsFail)
            return Error.Validation("REFLECTOR_STOP_ERROR", "Impossible d'arrêter le daemon svxreflector").ToFailure<Unit>();

        tracker.SetActiveReflector(null);

        logger.LogInformation("Reflector {ReflectorId} désactivé avec succès", command.Id);
        return unit.ToSuccess();
    }
}
