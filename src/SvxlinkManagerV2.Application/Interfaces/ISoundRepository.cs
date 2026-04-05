using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Repository pour la gestion des Sound
/// </summary>
public interface ISoundRepository
{
    Task<Validation<Error, Unit>> SaveAsync(
        SoundAggregate aggregate,
        CancellationToken cancellationToken = default);

    Task<Validation<Error, SoundAggregate>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime définitivement un Sound de la base de données (hard delete)
    /// </summary>
    Task<Validation<Error, Unit>> HardDeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
