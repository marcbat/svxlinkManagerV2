using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Repository pour la gestion du SA818 avec Event Sourcing.
/// Le SA818 possède un ID fixe car il n'existe qu'un seul device physique.
/// </summary>
public interface ISA818Repository
{
    /// <summary>
    /// Sauvegarde le SA818 (ajoute ses événements au stream)
    /// </summary>
    /// <param name="aggregate">Aggregate SA818 à sauvegarder</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, Unit>> SaveAsync(
        SA818Aggregate aggregate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge le SA818 depuis son stream d'événements.
    /// Utilise l'ID fixe SA818Aggregate.FixedId.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, SA818Aggregate>> GetAsync(
        CancellationToken cancellationToken = default);
}
