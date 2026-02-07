using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.RadioProfils.DeleteRadioProfil;

/// <summary>
/// Commande pour supprimer un RadioProfil
/// </summary>
/// <param name="Id">Identifiant du profil à supprimer</param>
public record DeleteRadioProfilCommand(Guid Id);

/// <summary>
/// Handler pour la commande DeleteRadioProfilCommand
/// </summary>
public static class DeleteRadioProfilCommandHandler
{
    /// <summary>
    /// Traite la commande de suppression d'un RadioProfil
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        DeleteRadioProfilCommand command,
        IRadioProfilRepository repository,
        CancellationToken cancellationToken)
    {
        // Charger l'aggregate depuis le stream
        var aggregateResult = await repository.GetByIdAsync(command.Id, cancellationToken);

        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        // Supprimer l'aggregate (soft delete)
        var deleteResult = aggregate.Delete();

        if (deleteResult.IsFail)
            return deleteResult;

        // Sauvegarder l'événement de suppression
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
