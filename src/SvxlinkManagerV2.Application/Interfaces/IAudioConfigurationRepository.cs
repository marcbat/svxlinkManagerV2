using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Persistance des niveaux ALSA mémorisés (aggregate unique, ID fixe).
/// </summary>
public interface IAudioConfigurationRepository
{
    /// <summary>
    /// Charge la configuration audio, ou une erreur NotFound si elle n'a jamais été initialisée.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, AudioConfigurationAggregate>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enregistre la configuration audio (création ou mise à jour).
    /// </summary>
    /// <param name="aggregate">Aggregate à persister.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, Unit>> SaveAsync(AudioConfigurationAggregate aggregate, CancellationToken cancellationToken = default);
}
