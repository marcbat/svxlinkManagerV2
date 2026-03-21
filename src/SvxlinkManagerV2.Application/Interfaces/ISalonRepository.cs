using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Repository pour la gestion des Salons avec Event Sourcing
/// </summary>
public interface ISalonRepository
{
    /// <summary>
    /// Sauvegarde un Salon (ajoute ses événements au stream)
    /// </summary>
    /// <param name="aggregate">Aggregate Salon à sauvegarder</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, Unit>> SaveAsync(
        SalonAggregate aggregate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge un Salon depuis son stream d'événements
    /// </summary>
    /// <param name="id">Identifiant du Salon</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, SalonAggregate>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère tous les Salons (via projection)
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<IReadOnlyList<SalonAggregate>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère le Salon par défaut (s'il existe)
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<SalonAggregate?> GetDefaultAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime un Salon (événement de suppression)
    /// </summary>
    /// <param name="id">Identifiant du Salon</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, Unit>> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
