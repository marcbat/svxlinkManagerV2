using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Reflectors.DeactivateReflector;

/// <summary>
/// Commande pour désactiver le Reflector (arrête le daemon svxreflector).
/// </summary>
/// <param name="Id">Identifiant unique du reflector à désactiver</param>
public record DeactivateReflectorCommand(Guid Id);

/// <summary>
/// Handler pour la commande DeactivateReflectorCommand
/// </summary>
public static class DeactivateReflectorCommandHandler
{
    /// <summary>
    /// Désactive le Reflector : marque l'état inactif dans l'event store.
    /// Le side-effect (arrêt daemon) est géré par ReflectorDeactivatedHandler.
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        DeactivateReflectorCommand command,
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

        // Désactivation (validation : bloqué si déjà inactif ou supprimé)
        var deactivateResult = aggregate.Deactivate();

        if (deactivateResult.IsFail)
            return deactivateResult;

        // Sauvegarde — déclenche l'event ReflectorDeactivated via Marten/Wolverine
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
