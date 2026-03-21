using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Reflectors.ActivateReflector;

/// <summary>
/// Commande pour activer le Reflector (démarre le daemon svxreflector).
/// </summary>
/// <param name="Id">Identifiant unique du reflector à activer</param>
public record ActivateReflectorCommand(Guid Id);

/// <summary>
/// Handler pour la commande ActivateReflectorCommand
/// </summary>
public static class ActivateReflectorCommandHandler
{
    /// <summary>
    /// Active le Reflector : marque l'état actif dans l'event store.
    /// Le side-effect (écriture config + démarrage daemon) est géré par ReflectorActivatedHandler.
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        ActivateReflectorCommand command,
        IReflectorRepository repository,
        CancellationToken cancellationToken)
    {
        // Chargement de l'aggregate
        var aggregateResult = await repository.GetByIdAsync(command.Id, cancellationToken);

        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        // Activation (validation : bloqué si déjà actif ou supprimé)
        var activateResult = aggregate.Activate();

        if (activateResult.IsFail)
            return activateResult;

        // Sauvegarde — déclenche l'event ReflectorActivated via Marten/Wolverine
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
