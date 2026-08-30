using LanguageExt;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Lecture et écriture des niveaux de la carte son de la machine hôte (mixage ALSA).
///
/// Deux contrôles seulement sont pilotés, désignés par la configuration : celui de capture
/// (audio venant du récepteur) et celui de restitution (audio partant vers l'émetteur).
/// Aucun autre contrôle de la carte n'est touché — le routage d'une carte son de nœud radio
/// est un réglage matériel délicat, dont l'application n'a pas à décider.
/// </summary>
public interface IAudioService
{
    /// <summary>
    /// Indique si les niveaux sont simulés (développement sans matériel).
    /// </summary>
    bool IsSimulated { get; }

    /// <summary>
    /// Lit l'état courant des deux contrôles pilotés.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, AudioMixerState>> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applique un niveau au contrôle de capture. La valeur est bornée à la plage réelle du contrôle.
    /// </summary>
    /// <param name="value">Niveau brut souhaité.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>L'état du contrôle après application.</returns>
    Task<Validation<Error, AudioControlState>> SetCaptureLevelAsync(int value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applique un niveau au contrôle de restitution. La valeur est bornée à la plage réelle du contrôle.
    /// </summary>
    /// <param name="value">Niveau brut souhaité.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>L'état du contrôle après application.</returns>
    Task<Validation<Error, AudioControlState>> SetPlaybackLevelAsync(int value, CancellationToken cancellationToken = default);
}
