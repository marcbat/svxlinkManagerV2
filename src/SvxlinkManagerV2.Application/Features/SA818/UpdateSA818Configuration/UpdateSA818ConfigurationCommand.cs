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

        // Si le SA818 n'existe pas encore, le créer avec les paramètres de la commande
        var aggregate = aggregateResult.Match(
            Succ: existing => existing,
            Fail: errors =>
            {
                // Créer un nouveau SA818 si non trouvé
                var createResult = SA818Aggregate.Create(
                    command.Volume,
                    command.Squelch,
                    command.Bandwidth,
                    command.PreEmph,
                    command.HighPass,
                    command.LowPass);

                return createResult.Match(
                    Succ: newAggregate => newAggregate,
                    Fail: createErrors => throw new InvalidOperationException(
                        $"Impossible de créer le SA818: {string.Join(", ", createErrors.Select(e => e.Message))}")
                );
            }
        );

        // Si le SA818 existe déjà (et que l'ID n'est pas vide), mettre à jour sa configuration
        if (aggregate.Id != Guid.Empty)
        {
            var updateResult = aggregate.UpdateConfiguration(
                command.Volume,
                command.Squelch,
                command.Bandwidth,
                command.PreEmph,
                command.HighPass,
                command.LowPass);

            // Si la mise à jour échoue, retourner les erreurs
            if (updateResult.IsFail)
                return updateResult;
        }

        // Sauvegarder l'aggregate (événements ajoutés)
        return await repository.SaveAsync(aggregate, cancellationToken);
    }
}
