namespace SvxlinkManagerV2.Infrastructure.Hardware;

/// <summary>
/// Options de la chaîne audio de la machine hôte (section « Audio »).
///
/// Les valeurs par défaut correspondent au codec H3 de l'Orange Pi Zero, plateforme cible :
/// « ADC Gain » commande le gain de l'ADC en réception, « Line Out » le niveau de sortie vers
/// l'émetteur, et le PTT est câblé sur gpio7 (cf. PTT_PIN dans svxlink.conf).
/// </summary>
public class AudioOptions
{
    public const string SectionName = "Audio";

    /// <summary>
    /// Active les implémentations simulées (développement sans carte son ni GPIO).
    /// </summary>
    public bool UseMock { get; set; }

    /// <summary>
    /// Index de la carte son pilotée (option <c>-c</c> d'amixer).
    /// </summary>
    public int CardIndex { get; set; }

    /// <summary>
    /// Nom du contrôle simple ALSA réglant le niveau de capture (audio venant du récepteur).
    /// </summary>
    public string CaptureControl { get; set; } = "ADC Gain";

    /// <summary>
    /// Nom du contrôle simple ALSA réglant le niveau de restitution (audio partant vers l'émetteur).
    /// </summary>
    public string PlaybackControl { get; set; } = "Line Out";

    /// <summary>
    /// Chemin du binaire amixer, ou son seul nom s'il est dans un répertoire standard.
    /// </summary>
    public string AmixerPath { get; set; } = "amixer";

    /// <summary>
    /// Délai maximum accordé à une commande amixer.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Racine sysfs des GPIO (doit correspondre à GPIO_PATH de svxlink.conf).
    /// </summary>
    public string PttGpioPath { get; set; } = "/sys/class/gpio";

    /// <summary>
    /// Nom du GPIO commandant le PTT (doit correspondre à PTT_PIN de svxlink.conf).
    /// </summary>
    public string PttPin { get; set; } = "gpio7";

    /// <summary>
    /// Durée par défaut d'un test d'émission, en secondes.
    /// </summary>
    public int PttTestDurationSeconds { get; set; } = 5;

    /// <summary>
    /// Durée maximale admise pour un test d'émission, en secondes. Garde-fou contre une
    /// émission prolongée déclenchée par erreur depuis l'interface.
    /// </summary>
    public int PttTestMaxDurationSeconds { get; set; } = 30;
}
