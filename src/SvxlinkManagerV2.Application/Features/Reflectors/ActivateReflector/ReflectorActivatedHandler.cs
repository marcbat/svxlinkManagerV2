using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;
using static LanguageExt.Prelude;
using static LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Application.Features.Reflectors.ActivateReflector;

/// <summary>
/// Handler Wolverine qui réagit à l'événement ReflectorActivated (side-effect).
/// Orchestre :
/// 1. Écriture du fichier svxreflector.conf depuis la config de l'aggregate
/// 2. Démarrage du daemon svxreflector
/// </summary>
public static class ReflectorActivatedHandler
{
    private const string ReflectorConfigPath = "/etc/svxlink/svxreflector.conf";

    /// <summary>
    /// Traite l'événement ReflectorActivated
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        ReflectorActivated @event,
        IReflectorRepository reflectorRepository,
        IReflectorConfigurationService configurationService,
        IReflectorDaemonService daemonService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Début du side-effect ReflectorActivated pour Reflector {ReflectorId}",
            @event.Id);

        try
        {
            // Étape 1 : Charger l'aggregate reflector
            logger.LogDebug("Chargement de l'aggregate Reflector {ReflectorId}", @event.Id);
            var reflectorResult = await reflectorRepository.GetByIdAsync(@event.Id, cancellationToken);

            if (reflectorResult.IsFail)
            {
                logger.LogError("Impossible de charger le Reflector {ReflectorId}", @event.Id);
                return reflectorResult.Match(
                    Succ: _ => throw new InvalidOperationException(),
                    Fail: errors => Validation<Error, Unit>.Fail(
                        errors.Map(e => New(e.Code, e.Message))));
            }

            var reflector = reflectorResult.Match(
                Succ: r => r,
                Fail: _ => throw new InvalidOperationException());

            logger.LogDebug("Reflector {ReflectorName} chargé avec succès", reflector.Name);

            // Étape 2 : Écrire le fichier svxreflector.conf
            logger.LogInformation(
                "Écriture du fichier de configuration reflector : {Path}",
                ReflectorConfigPath);

            var configResult = await configurationService.WriteConfigAsync(
                reflector,
                ReflectorConfigPath,
                cancellationToken);

            if (configResult.IsFail)
            {
                logger.LogError("Échec de l'écriture du fichier svxreflector.conf");
                return configResult;
            }

            logger.LogInformation("Fichier svxreflector.conf écrit avec succès");

            // Étape 3 : Démarrer (ou redémarrer) le daemon svxreflector
            logger.LogInformation("Démarrage du daemon svxreflector");
            var daemonResult = await daemonService.RestartAsync(cancellationToken);

            if (daemonResult.IsFail)
            {
                logger.LogError("Échec du démarrage du daemon svxreflector");
                return daemonResult;
            }

            logger.LogInformation(
                "Side-effect ReflectorActivated terminé avec succès pour Reflector {ReflectorId}",
                @event.Id);

            return unit;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception dans le side-effect ReflectorActivated");
            return Validation<Error, Unit>.Fail(Seq1(New(ex)));
        }
    }
}
