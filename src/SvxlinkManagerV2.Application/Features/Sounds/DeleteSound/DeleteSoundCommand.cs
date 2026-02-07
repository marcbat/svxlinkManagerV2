using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Sounds.DeleteSound;

/// <summary>
/// Commande pour supprimer un Sound
/// </summary>
/// <param name="Id">Identifiant du Sound à supprimer</param>
public record DeleteSoundCommand(Guid Id);

/// <summary>
/// Handler pour la commande DeleteSoundCommand
/// </summary>
public static class DeleteSoundCommandHandler
{
    /// <summary>
    /// Traite la commande de suppression d'un Sound
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        DeleteSoundCommand command,
        ISoundRepository repository,
        CancellationToken cancellationToken)
    {
        // Charger l'aggregate
        var aggregateResult = await repository.GetByIdAsync(command.Id, cancellationToken);

        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        // Suppression logique
        var deleteResult = aggregate.Delete();

        if (deleteResult.IsFail)
            return deleteResult;

        // Sauvegarde
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
