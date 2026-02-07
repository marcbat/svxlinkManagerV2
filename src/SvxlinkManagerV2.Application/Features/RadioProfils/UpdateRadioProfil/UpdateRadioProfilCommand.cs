using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.RadioProfils.UpdateRadioProfil;

/// <summary>
/// Commande pour mettre à jour un RadioProfil existant
/// </summary>
/// <param name="Id">Identifiant du profil à mettre à jour</param>
/// <param name="Name">Nouveau nom (optionnel)</param>
/// <param name="RxConfiguration">Nouvelle configuration Rx (optionnel)</param>
/// <param name="TxConfiguration">Nouvelle configuration Tx (optionnel)</param>
public record UpdateRadioProfilCommand(
    Guid Id,
    string? Name = null,
    RxConfiguration? RxConfiguration = null,
    TxConfiguration? TxConfiguration = null);

/// <summary>
/// Handler pour la commande UpdateRadioProfilCommand
/// </summary>
public static class UpdateRadioProfilCommandHandler
{
    /// <summary>
    /// Traite la commande de mise à jour d'un RadioProfil
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        UpdateRadioProfilCommand command,
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

        // Mettre à jour l'aggregate
        var updateResult = aggregate.Update(
            command.Name,
            command.RxConfiguration,
            command.TxConfiguration);

        if (updateResult.IsFail)
            return updateResult;

        // Sauvegarder les nouveaux événements
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
