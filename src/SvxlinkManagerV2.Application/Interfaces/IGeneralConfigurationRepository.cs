using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Repository pour la gestion de la configuration générale avec Event Sourcing.
/// Il n'existe qu'une seule instance (ID fixe).
/// </summary>
public interface IGeneralConfigurationRepository
{
    /// <summary>
    /// Sauvegarde la configuration générale (ajoute ses événements au stream Marten).
    /// </summary>
    Task<Validation<Error, Unit>> SaveAsync(
        GeneralConfigurationAggregate aggregate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge la configuration générale depuis son stream d'événements.
    /// Retourne null si aucune configuration n'a encore été créée.
    /// </summary>
    Task<GeneralConfigurationAggregate?> GetAsync(
        CancellationToken cancellationToken = default);
}
