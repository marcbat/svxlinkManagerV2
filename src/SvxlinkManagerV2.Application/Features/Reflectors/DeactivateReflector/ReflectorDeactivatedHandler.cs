using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;
using static LanguageExt.Prelude;
using static LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Application.Features.Reflectors.DeactivateReflector;

/// <summary>
/// Handler Wolverine qui réagit à l'événement ReflectorDeactivated (side-effect).
/// Arrête le daemon svxreflector.
/// </summary>
public static class ReflectorDeactivatedHandler
{
    /// <summary>
    /// Traite l'événement ReflectorDeactivated
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        ReflectorDeactivated @event,
        IReflectorDaemonService daemonService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Début du side-effect ReflectorDeactivated pour Reflector {ReflectorId}",
            @event.Id);

        try
        {
            logger.LogInformation("Arrêt du daemon svxreflector");
            var result = await daemonService.StopAsync(cancellationToken);

            if (result.IsFail)
                logger.LogError("Échec de l'arrêt du daemon svxreflector");
            else
                logger.LogInformation(
                    "Side-effect ReflectorDeactivated terminé avec succès pour Reflector {ReflectorId}",
                    @event.Id);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception dans le side-effect ReflectorDeactivated");
            return Validation<Error, Unit>.Fail(Seq1(New(ex)));
        }
    }
}
