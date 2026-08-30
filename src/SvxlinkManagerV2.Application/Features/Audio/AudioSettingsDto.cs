namespace SvxlinkManagerV2.Application.Features.Audio;

/// <summary>
/// Niveau d'un contrôle de mixage ALSA, prêt à être affiché.
/// </summary>
/// <param name="ControlName">Nom du contrôle simple ALSA (ex. « Line Out »).</param>
/// <param name="Value">Valeur brute courante.</param>
/// <param name="MinValue">Borne basse de l'échelle du contrôle.</param>
/// <param name="MaxValue">Borne haute de l'échelle du contrôle.</param>
/// <param name="Percent">Position du niveau dans son échelle, en pourcentage.</param>
public record AudioLevelDto(string ControlName, int Value, int MinValue, int MaxValue, int Percent);

/// <summary>
/// Couple de niveaux retournés après application d'un réglage.
/// </summary>
/// <param name="Capture">Niveau de capture effectivement retenu par le pilote.</param>
/// <param name="Playback">Niveau de restitution effectivement retenu par le pilote.</param>
public record AudioLevelsDto(AudioLevelDto Capture, AudioLevelDto Playback);

/// <summary>
/// État du test d'émission tel que présenté à l'interface.
/// </summary>
/// <param name="IsTransmitting">Vrai tant que le PTT est maintenu.</param>
/// <param name="RemainingSeconds">Secondes restantes avant le relâchement automatique.</param>
/// <param name="IsSimulated">Vrai lorsque le PTT est simulé.</param>
/// <param name="CanStart">Vrai si un test peut être déclenché maintenant.</param>
/// <param name="BlockedReason">Motif du refus quand <paramref name="CanStart"/> est faux.</param>
public record PttTestStatusDto(
    bool IsTransmitting,
    int RemainingSeconds,
    bool IsSimulated,
    bool CanStart,
    string? BlockedReason);

/// <summary>
/// Indicateurs de la qualité du signal reçu.
/// </summary>
/// <param name="Rssi">RSSI brut rapporté par le module SA818 (0-255), null s'il est illisible.</param>
/// <param name="RssiError">Motif d'indisponibilité du RSSI, null s'il a été lu.</param>
/// <param name="LastDistortionAt">Dernier écrêtage d'entrée signalé par SVXLink, null si aucun.</param>
/// <param name="DistortionCount">Nombre d'écrêtages signalés depuis la dernière remise à zéro.</param>
public record RxSignalDto(
    int? Rssi,
    string? RssiError,
    DateTimeOffset? LastDistortionAt,
    int DistortionCount)
{
    /// <summary>
    /// Borne haute de l'échelle brute du RSSI du SA818.
    /// </summary>
    public const int RssiMax = 255;

    /// <summary>
    /// Position du RSSI dans son échelle, en pourcentage, ou null s'il est illisible.
    /// </summary>
    public int? RssiPercent => Rssi is null
        ? null
        : (int)Math.Round(Math.Clamp(Rssi.Value, 0, RssiMax) * 100.0 / RssiMax);
}

/// <summary>
/// Vue complète de la page de réglage audio.
/// Chaque bloc est indépendant : une carte son illisible n'empêche pas d'afficher le reste.
/// </summary>
public record AudioSettingsDto
{
    /// <summary>
    /// Niveau de capture (audio venant du récepteur), null si la carte son est illisible.
    /// </summary>
    public AudioLevelDto? Capture { get; init; }

    /// <summary>
    /// Niveau de restitution (audio partant vers l'émetteur), null si la carte son est illisible.
    /// </summary>
    public AudioLevelDto? Playback { get; init; }

    /// <summary>
    /// Motif d'indisponibilité des niveaux, null s'ils ont été lus.
    /// </summary>
    public string? LevelsError { get; init; }

    /// <summary>
    /// Vrai lorsque les niveaux proviennent du mock de développement.
    /// </summary>
    public bool IsSimulated { get; init; }

    /// <summary>
    /// État du test d'émission.
    /// </summary>
    public required PttTestStatusDto Ptt { get; init; }

    /// <summary>
    /// Durée proposée par défaut pour un test d'émission, en secondes.
    /// </summary>
    public int DefaultTestDurationSeconds { get; init; }

    /// <summary>
    /// Durée maximale admise pour un test d'émission, en secondes.
    /// </summary>
    public int MaxTestDurationSeconds { get; init; }
}
