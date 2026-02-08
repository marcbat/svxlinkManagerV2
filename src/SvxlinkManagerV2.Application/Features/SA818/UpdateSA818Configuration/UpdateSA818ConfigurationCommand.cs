using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.SA818.UpdateSA818Configuration;

/// <summary>
/// Commande pour mettre à jour la configuration globale du module SA818.
/// Le SA818 possède un ID fixe car il n'existe qu'un seul device physique.
/// </summary>
/// <param name="Volume">Volume audio (plage valide: 1-8)</param>
/// <param name="Squelch">Niveau de squelch (plage valide: 0-8)</param>
/// <param name="Bandwidth">Largeur de bande (12.5kHz ou 25kHz)</param>
/// <param name="PreEmph">Activation du filtre de pré-accentuation audio</param>
/// <param name="HighPass">Activation du filtre passe-haut</param>
/// <param name="LowPass">Activation du filtre passe-bas</param>
public record UpdateSA818ConfigurationCommand(
    int Volume,
    int Squelch,
    SA818Bandwidth Bandwidth,
    bool PreEmph,
    bool HighPass,
    bool LowPass);

/// <summary>
/// Handler pour la commande UpdateSA818ConfigurationCommand.
/// Charge ou crée le SA818Aggregate (ID fixe), met à jour sa configuration,
/// et persiste les événements.
/// </summary>
public static class UpdateSA818ConfigurationCommandHandler
{
    /// <summary>
    /// Traite la commande de mise à jour de la configuration du SA818.
    /// Si le SA818 n'existe pas encore, il est créé avec les paramètres fournis.
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        UpdateSA818ConfigurationCommand command,
        ISA818Repository repository,
        CancellationToken cancellationToken)
    {
        // Tenter de charger le SA818 existant
        var aggregateResult = await repository.GetAsync(cancellationToken);

        // Si le SA818 n'existe pas, le créer avec les paramètres de la commande
        // Si le SA818 existe, le mettre à jour
        return await aggregateResult.Match(
            Succ: async existing =>
            {
                // SA818 existe déjà, mettre à jour sa configuration
                var updateResult = existing.UpdateConfiguration(
                    command.Volume,
                    command.Squelch,
                    command.Bandwidth,
                    command.PreEmph,
                    command.HighPass,
                    command.LowPass);

                // Si la mise à jour échoue, retourner les erreurs
                if (updateResult.IsFail)
                    return updateResult;

                // Sauvegarder l'aggregate (événements ajoutés)
                return await repository.SaveAsync(existing, cancellationToken);
            },
            Fail: async errors =>
            {
                // SA818 n'existe pas, le créer
                var createResult = SA818Aggregate.Create(
                    command.Volume,
                    command.Squelch,
                    command.Bandwidth,
                    command.PreEmph,
                    command.HighPass,
                    command.LowPass);

                // Si la création échoue (validations), retourner les erreurs
                if (createResult.IsFail)
                    return createResult.Map(_ => unit);

                // Sauvegarder le nouvel aggregate
                var newAggregate = createResult.Match(
                    Succ: a => a,
                    Fail: _ => throw new InvalidOperationException("Should not happen")
                );

                return await repository.SaveAsync(newAggregate, cancellationToken);
            }
        );
    }
}
