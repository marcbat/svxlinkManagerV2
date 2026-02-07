using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Sounds.CreateSound;

/// <summary>
/// Commande pour créer un nouveau Sound (fichier audio WAV)
/// </summary>
/// <param name="Id">Identifiant unique du sound</param>
/// <param name="Name">Nom du fichier</param>
/// <param name="FileContent">Contenu du fichier WAV</param>
public record CreateSoundCommand(
    Guid Id,
    string Name,
    byte[] FileContent);

/// <summary>
/// Handler pour la commande CreateSoundCommand
/// </summary>
public static class CreateSoundCommandHandler
{
    /// <summary>
    /// Traite la commande de création d'un Sound
    /// </summary>
    public static async Task<Validation<Error, Guid>> Handle(
        CreateSoundCommand command,
        ISoundRepository repository,
        CancellationToken cancellationToken)
    {
        // Création de l'aggregate avec validations métier
        var aggregateResult = SoundAggregate.Create(
            command.Id,
            command.Name,
            command.FileContent);

        // Si la création échoue, retourner les erreurs
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Guid>.Fail(errors));

        // Sauvegarde de l'aggregate
        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        var saveResult = await repository.SaveAsync(aggregate, cancellationToken);

        // Retour de l'ID si succès
        return saveResult.Match(
            Succ: _ => Validation<Error, Guid>.Success(aggregate.Id),
            Fail: errors => Validation<Error, Guid>.Fail(errors));
    }
}
