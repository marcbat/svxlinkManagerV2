using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Sounds.UpdateSound;

/// <summary>
/// Commande pour mettre à jour un Sound existant
/// </summary>
/// <param name="Id">Identifiant du Sound à mettre à jour</param>
/// <param name="Name">Nouveau nom (optionnel)</param>
/// <param name="FileContent">Nouveau contenu WAV (optionnel)</param>
public record UpdateSoundCommand(
    Guid Id,
    string? Name = null,
    byte[]? FileContent = null);

/// <summary>
/// Handler pour la commande UpdateSoundCommand
/// </summary>
public static class UpdateSoundCommandHandler
{
    /// <summary>
    /// Traite la commande de mise à jour d'un Sound
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        UpdateSoundCommand command,
        ISoundRepository repository,
        CancellationToken cancellationToken)
    {
        // Charger l'aggregate depuis le repository
        var aggregateResult = await repository.GetByIdAsync(command.Id, cancellationToken);

        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        // Mise à jour de l'aggregate
        var updateResult = aggregate.Update(command.Name, command.FileContent);

        if (updateResult.IsFail)
            return updateResult;

        // Sauvegarde
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
