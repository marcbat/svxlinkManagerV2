using LanguageExt;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Reflectors.ActivateReflector;

/// <summary>
/// Commande pour activer le Reflector (démarre le daemon svxreflector).
/// </summary>
/// <param name="Id">Identifiant unique du reflector à activer</param>
public record ActivateReflectorCommand(Guid Id);

/// <summary>
/// Handler pour la commande ActivateReflectorCommand.
/// Orchestre l'écriture du fichier svxreflector.conf et le démarrage du daemon.
/// </summary>
public static class ActivateReflectorCommandHandler
{
    private const string ReflectorConfigPath = "/etc/svxlink/svxreflector.conf";

    /// <summary>
    /// Active le Reflector : écrit la configuration, démarre le daemon
    /// et met à jour le tracker d'état runtime.
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        ActivateReflectorCommand command,
        IReflectorRepository repository,
        IActiveSessionTracker tracker,
        IReflectorConfigurationService configurationService,
        IReflectorDaemonService daemonService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Activation du Reflector {ReflectorId}", command.Id);

        // Étape 1 : Charger l'aggregate
        var aggregateResult = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        if (aggregate.IsDeleted)
            return Error.Validation("REFLECTOR_DELETED", "Le reflector est supprimé").ToFailure<Unit>();

        // Étape 2 : Écrire le fichier svxreflector.conf
        logger.LogInformation("Ecriture du fichier {Path}", ReflectorConfigPath);
        var configResult = await configurationService.WriteConfigAsync(aggregate, ReflectorConfigPath, cancellationToken);
        if (configResult.IsFail)
            return Error.Validation("REFLECTOR_CONFIG_ERROR", "Impossible d'écrire le fichier svxreflector.conf").ToFailure<Unit>();

        // Étape 3 : Démarrer le daemon svxreflector
        logger.LogInformation("Démarrage du daemon svxreflector");
        var daemonResult = await daemonService.RestartAsync(cancellationToken);
        if (daemonResult.IsFail)
            return Error.Validation("REFLECTOR_DAEMON_ERROR", "Impossible de démarrer le daemon svxreflector").ToFailure<Unit>();

        // Étape 4 : Mettre à jour le tracker d'état runtime
        tracker.SetActiveReflector(command.Id);

        logger.LogInformation("Reflector {ReflectorName} ({ReflectorId}) activé avec succès", aggregate.Name, command.Id);
        return unit.ToSuccess();
    }
}
