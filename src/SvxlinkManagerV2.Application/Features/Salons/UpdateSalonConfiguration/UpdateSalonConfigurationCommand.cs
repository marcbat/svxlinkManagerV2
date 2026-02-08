using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.UpdateSalonConfiguration;

/// <summary>
/// Commande pour mettre à jour la configuration d'un Salon
/// </summary>
/// <param name="Id">Identifiant unique du salon</param>
/// <param name="RxFrequency">Fréquence de réception en MHz (ex: 145.550)</param>
/// <param name="TxFrequency">Fréquence de transmission en MHz (ex: 145.550)</param>
/// <param name="RxCtcss">Tonalité CTCSS de réception en Hz (ex: 136.5). Null = aucun CTCSS</param>
/// <param name="TxCtcss">Tonalité CTCSS de transmission en Hz (ex: 136.5). Null = aucun CTCSS</param>
/// <param name="Configuration">Nouvelle configuration SVXLink complète</param>
public record UpdateSalonConfigurationCommand(
    Guid Id,
    decimal RxFrequency,
    decimal TxFrequency,
    decimal? RxCtcss,
    decimal? TxCtcss,
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

        // Construction de la configuration complète avec les fréquences radio
        var configurationWithRadio = command.Configuration with
        {
            RxFrequency = command.RxFrequency,
            TxFrequency = command.TxFrequency,
            RxCtcss = command.RxCtcss,
            TxCtcss = command.TxCtcss
        };

        // Mise à jour de la configuration
        var updateResult = aggregate.UpdateConfiguration(configurationWithRadio);

        if (updateResult.IsFail)
            return updateResult;

        // Sauvegarde de l'aggregate
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
