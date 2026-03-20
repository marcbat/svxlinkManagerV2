using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;

/// <summary>
/// Commande pour activer un Salon (connexion au reflector)
/// </summary>
/// <param name="Id">Identifiant unique du salon à activer</param>
public record ActivateSalonCommand(Guid Id);

/// <summary>
/// Handler pour la commande ActivateSalonCommand
/// </summary>
public static class ActivateSalonCommandHandler
{
    /// <summary>
    /// Traite la commande d'activation d'un Salon.
    /// Règle métier : un seul salon peut être actif à la fois.
    /// Si un autre salon est déjà actif, il est désactivé automatiquement.
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        ActivateSalonCommand command,
        ISalonRepository repository,
        CancellationToken cancellationToken)
    {
        // Récupération de l'aggregate
        var aggregateResult = await repository.GetByIdAsync(command.Id, cancellationToken);

        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        // Règle métier : désactiver le salon actuellement actif (s'il en existe un autre)
        var currentActive = await repository.GetActiveAsync(cancellationToken);
        if (currentActive != null && currentActive.Id != aggregate.Id)
        {
            var deactivateResult = currentActive.Deactivate();
            if (deactivateResult.IsFail)
                return deactivateResult;

            var saveOldResult = await repository.SaveAsync(currentActive, cancellationToken);
            if (saveOldResult.IsFail)
                return saveOldResult;
        }

        // Activation du salon
        var activateResult = aggregate.Activate();

        if (activateResult.IsFail)
            return activateResult;

        // Sauvegarde de l'aggregate
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
