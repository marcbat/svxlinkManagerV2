namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Détection de la saturation de l'audio en réception.
///
/// Le périphérique de capture ALSA est ouvert en exclusivité par SVXLink dès qu'un salon tourne :
/// l'application ne peut donc pas mesurer le niveau d'entrée elle-même. C'est SVXLink qui joue ce
/// rôle, via son option <c>PEAK_METER</c> activée sur le récepteur : il signale dans ses logs
/// chaque écrêtage constaté sur l'entrée. Ce service ne fait qu'en tenir le compte pour l'interface.
/// </summary>
public interface IRxDistortionService
{
    /// <summary>
    /// Instant du dernier écrêtage signalé, ou null si aucun depuis le démarrage ou la remise à zéro.
    /// </summary>
    DateTimeOffset? LastDetectedAt { get; }

    /// <summary>
    /// Nombre d'écrêtages signalés depuis le démarrage ou la dernière remise à zéro.
    /// </summary>
    int DetectionCount { get; }

    /// <summary>
    /// Émis à chaque écrêtage signalé par SVXLink.
    /// </summary>
    event Action<DateTimeOffset>? OnDistortionDetected;

    /// <summary>
    /// Remet le compteur à zéro, pour repartir d'un état propre après un réglage de niveau.
    /// </summary>
    void Reset();
}
