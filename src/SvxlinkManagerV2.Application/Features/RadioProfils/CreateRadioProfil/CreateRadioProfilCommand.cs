using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.RadioProfils.CreateRadioProfil;

/// <summary>
/// Commande pour créer un nouveau RadioProfil
/// </summary>
/// <param name="Id">Identifiant unique du profil</param>
/// <param name="Name">Nom du profil radio</param>
/// <param name="RxConfiguration">Configuration de réception</param>
/// <param name="TxConfiguration">Configuration de transmission</param>
public record CreateRadioProfilCommand(
    Guid Id,
    string Name,
    RxConfiguration RxConfiguration,
    TxConfiguration TxConfiguration);

/// <summary>
/// Handler pour la commande CreateRadioProfilCommand
/// </summary>
public static class CreateRadioProfilCommandHandler
{
    /// <summary>
    /// Traite la commande de création d'un RadioProfil
    /// </summary>
    public static async Task<Validation<Error, Guid>> Handle(
        CreateRadioProfilCommand command,
        IRadioProfilRepository repository,
        CancellationToken cancellationToken)
    {
        // Création de l'aggregate avec validations
        var aggregateResult = RadioProfilAggregate.Create(
            command.Id,
            command.Name,
            command.RxConfiguration,
            command.TxConfiguration);

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
