using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.DeactivateSalon;

/// <summary>
/// Commande pour désactiver un Salon (déconnexion du reflector)
/// </summary>
/// <param name="Id">Identifiant unique du salon à désactiver</param>
public record DeactivateSalonCommand(Guid Id);

/// <summary>
/// Handler pour la commande DeactivateSalonCommand
/// </summary>
public static class DeactivateSalonCommandHandler
{
    /// <summary>
    /// Traite la commande de désactivation d'un Salon
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        DeactivateSalonCommand command,
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

        // Désactivation du salon
        var deactivateResult = aggregate.Deactivate();

        if (deactivateResult.IsFail)
            return deactivateResult;

        // Sauvegarde de l'aggregate
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
