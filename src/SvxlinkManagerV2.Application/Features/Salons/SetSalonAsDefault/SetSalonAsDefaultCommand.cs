using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Salons.SetSalonAsDefault;

/// <summary>
/// Commande pour désigner un Salon comme salon par défaut.
/// Un seul salon peut être le salon par défaut à la fois.
/// Si un autre salon est déjà par défaut, il perd ce statut automatiquement.
/// </summary>
/// <param name="Id">Identifiant unique du salon à définir par défaut</param>
public record SetSalonAsDefaultCommand(Guid Id);

/// <summary>
/// Handler pour la commande SetSalonAsDefaultCommand
/// </summary>
public static class SetSalonAsDefaultCommandHandler
{
    /// <summary>
    /// Traite la commande de désignation d'un Salon comme salon par défaut.
    /// Règle métier : un seul salon peut être le salon par défaut.
    /// L'ancien salon par défaut perd automatiquement ce statut.
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        SetSalonAsDefaultCommand command,
        ISalonRepository repository,
        CancellationToken cancellationToken)
    {
        // Récupération de l'aggregate cible
        var aggregateResult = await repository.GetByIdAsync(command.Id, cancellationToken);

        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        // Règle métier : si le salon est déjà par défaut, rien à faire
        if (aggregate.IsDefault)
            return unit.ToSuccess();

        // Règle métier : unsetter l'ancien salon par défaut (s'il existe)
        var currentDefault = await repository.GetDefaultAsync(cancellationToken);
        if (currentDefault != null)
        {
            var unsetResult = currentDefault.UnsetDefault();
            if (unsetResult.IsFail)
                return unsetResult;

            var saveOldResult = await repository.SaveAsync(currentDefault, cancellationToken);
            if (saveOldResult.IsFail)
                return saveOldResult;
        }

        // Désigner le nouveau salon par défaut
        var setResult = aggregate.SetAsDefault();
        if (setResult.IsFail)
            return setResult;

        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
