using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Events;
using static LanguageExt.Prelude;
using static LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Application.Features.Salons.DeactivateSalon;

/// <summary>
/// Handler Wolverine qui réagit à l'événement SalonDeactivated.
/// Arrête le daemon SVXLink pour déconnecter le nœud du reflector.
/// </summary>
public static class SalonDeactivatedHandler
{
    /// <summary>
    /// Traite l'événement SalonDeactivated (side-effect).
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        SalonDeactivated @event,
        ISvxLinkDaemonService daemonService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Début du side-effect SalonDeactivated pour Salon {SalonId}",
            @event.Id);

        try
        {
            logger.LogInformation("Arrêt du daemon SVXLink");
            var stopResult = await daemonService.StopAsync(cancellationToken);

            if (stopResult.IsFail)
            {
                logger.LogError("Échec de l'arrêt du daemon SVXLink");
                return stopResult;
            }

            logger.LogInformation(
                "Side-effect SalonDeactivated terminé avec succès pour Salon {SalonId}",
                @event.Id);

            return unit;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Erreur inattendue lors du traitement de SalonDeactivated pour Salon {SalonId}",
                @event.Id);

            return Validation<Error, Unit>.Fail(
                Seq1(New("SALON_DEACTIVATED_HANDLER_ERROR", ex)));
        }
    }
}
