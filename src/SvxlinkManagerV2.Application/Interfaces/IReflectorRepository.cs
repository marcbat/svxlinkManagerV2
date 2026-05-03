using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Repository pour la gestion du Reflector avec Event Sourcing
/// </summary>
public interface IReflectorRepository
{
    /// <summary>
    /// Sauvegarde un Reflector (ajoute ses événements au stream)
    /// </summary>
    /// <param name="aggregate">Aggregate Reflector à sauvegarder</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, Unit>> SaveAsync(
        ReflectorAggregate aggregate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge un Reflector depuis son stream d'événements
    /// </summary>
    /// <param name="id">Identifiant du Reflector</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, ReflectorAggregate>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère tous les Reflectors (via projection)
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<IReadOnlyList<ReflectorAggregate>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime un Reflector (événement de suppression)
    /// </summary>
    /// <param name="id">Identifiant du Reflector</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, Unit>> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
