using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.DeleteSalon;

/// <summary>
/// Commande pour supprimer un Salon (soft delete)
/// </summary>
/// <param name="Id">Identifiant unique du salon à supprimer</param>
public record DeleteSalonCommand(Guid Id);

/// <summary>
/// Handler pour la commande DeleteSalonCommand
/// </summary>
public static class DeleteSalonCommandHandler
{
    /// <summary>
    /// Traite la commande de suppression d'un Salon
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        DeleteSalonCommand command,
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

        // Suppression du salon
        var deleteResult = aggregate.Delete();

        if (deleteResult.IsFail)
            return deleteResult;

        // Sauvegarde de l'aggregate
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
