using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.CreateSalon;

/// <summary>
/// Commande pour créer un nouveau Salon
/// </summary>
/// <param name="Id">Identifiant unique du salon</param>
/// <param name="Name">Nom du salon</param>
/// <param name="IsDefault">Si c'est le salon par défaut</param>
/// <param name="IsTemporized">Si le salon est temporisé</param>
/// <param name="Configuration">Configuration SVXLink complète</param>
public record CreateSalonCommand(
    Guid Id,
    string Name,
    bool IsDefault,
    bool IsTemporized,
    SvxLinkConfiguration Configuration);

/// <summary>
/// Handler pour la commande CreateSalonCommand
/// </summary>
public static class CreateSalonCommandHandler
{
    /// <summary>
    /// Traite la commande de création d'un Salon
    /// </summary>
    public static async Task<Validation<Error, Guid>> Handle(
        CreateSalonCommand command,
        ISalonRepository repository,
        CancellationToken cancellationToken)
    {
        // Création de l'aggregate avec validations
        var aggregateResult = SalonAggregate.Create(
            command.Id,
            command.Name,
            command.IsDefault,
            command.IsTemporized,
            command.Configuration);

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
