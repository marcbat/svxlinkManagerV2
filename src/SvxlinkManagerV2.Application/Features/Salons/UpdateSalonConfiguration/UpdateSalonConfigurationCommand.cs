using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.UpdateSalonConfiguration;

/// <summary>
/// Commande pour mettre à jour la configuration d'un Salon
/// </summary>
/// <param name="Id">Identifiant unique du salon</param>
/// <param name="Configuration">Nouvelle configuration SVXLink complète</param>
public record UpdateSalonConfigurationCommand(
    Guid Id,
    SvxLinkConfiguration Configuration);

/// <summary>
/// Handler pour la commande UpdateSalonConfigurationCommand
/// </summary>
public static class UpdateSalonConfigurationCommandHandler
{
    /// <summary>
    /// Traite la commande de mise à jour de la configuration d'un Salon
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        UpdateSalonConfigurationCommand command,
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

        // Mise à jour de la configuration
        var updateResult = aggregate.UpdateConfiguration(command.Configuration);

        if (updateResult.IsFail)
            return updateResult;

        // Sauvegarde de l'aggregate
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
