using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Repository pour la gestion des Sound avec Event Sourcing
/// </summary>
public interface ISoundRepository
{
    /// <summary>
    /// Sauvegarde un Sound (ajoute ses événements au stream)
    /// </summary>
    /// <param name="aggregate">Aggregate Sound à sauvegarder</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, Unit>> SaveAsync(
        SoundAggregate aggregate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge un Sound depuis son stream d'événements
    /// </summary>
    /// <param name="id">Identifiant du Sound</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, SoundAggregate>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère tous les Sounds (via projection)
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<IReadOnlyList<SoundAggregate>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime un Sound (événement de suppression)
    /// </summary>
    /// <param name="id">Identifiant du Sound</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, Unit>> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
