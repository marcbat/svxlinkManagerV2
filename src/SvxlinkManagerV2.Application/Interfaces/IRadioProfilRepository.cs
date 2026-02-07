using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Repository pour la gestion des RadioProfil avec Event Sourcing
/// </summary>
public interface IRadioProfilRepository
{
    /// <summary>
    /// Sauvegarde un RadioProfil (ajoute ses événements au stream)
    /// </summary>
    /// <param name="aggregate">Aggregate RadioProfil à sauvegarder</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, Unit>> SaveAsync(
        RadioProfilAggregate aggregate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge un RadioProfil depuis son stream d'événements
    /// </summary>
    /// <param name="id">Identifiant du RadioProfil</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, RadioProfilAggregate>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère tous les RadioProfils (via projection)
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<IReadOnlyList<RadioProfilAggregate>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime un RadioProfil (événement de suppression)
    /// </summary>
    /// <param name="id">Identifiant du RadioProfil</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    Task<Validation<Error, Unit>> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
