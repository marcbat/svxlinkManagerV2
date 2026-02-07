using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Entities;

/// <summary>
/// Configuration de réception pour un RadioProfil.
/// Représente tous les paramètres de la section [Rx1] du fichier svxlink.conf.
/// </summary>
public class RxConfiguration : Entity<Guid>
{
    /// <summary>
    /// Constructeur par défaut requis pour Marten (rehydratation)
    /// </summary>
    public RxConfiguration() : base()
    {
    }

    /// <summary>
    /// Constructeur avec tous les paramètres
    /// </summary>
    public RxConfiguration(
        Guid id,
        string type,
        string audioDev,
        int audioChannel,
        string sqlDet,
        int sqlStartDelay,
        int sqlDelay,
        int sqlHangtime,
        int sqlExtendedHangtime,
        decimal? ctcssFq,
        int ctcssThresh)
        : base(id)
    {
        Type = type;
        AudioDev = audioDev;
        AudioChannel = audioChannel;
        SqlDet = sqlDet;
        SqlStartDelay = sqlStartDelay;
        SqlDelay = sqlDelay;
        SqlHangtime = sqlHangtime;
        SqlExtendedHangtime = sqlExtendedHangtime;
        CtcssFq = ctcssFq;
        CtcssThresh = ctcssThresh;
    }

    /// <summary>
    /// Type de récepteur (ex: "Local")
    /// </summary>
    public string Type { get; private set; } = "Local";

    /// <summary>
    /// Périphérique audio (ex: "alsa:plughw:0")
    /// </summary>
    public string AudioDev { get; private set; } = "alsa:plughw:0";

    /// <summary>
    /// Canal audio (0 = gauche, 1 = droite)
    /// </summary>
    public int AudioChannel { get; private set; } = 0;

    /// <summary>
    /// Type de détection de squelch (GPIO, VOX, CTCSS, SERIAL, EVDEV)
    /// </summary>
    public string SqlDet { get; private set; } = "GPIO";

    /// <summary>
    /// Délai de démarrage du squelch en millisecondes
    /// </summary>
    public int SqlStartDelay { get; private set; } = 500;

    /// <summary>
    /// Délai du squelch en millisecondes
    /// </summary>
    public int SqlDelay { get; private set; } = 150;

    /// <summary>
    /// Temps de maintien du squelch en millisecondes
    /// </summary>
    public int SqlHangtime { get; private set; } = 20;

    /// <summary>
    /// Temps de maintien étendu du squelch en millisecondes
    /// </summary>
    public int SqlExtendedHangtime { get; private set; } = 1000;

    /// <summary>
    /// Fréquence CTCSS en Hz (ex: 71.9, 136.5). Null si pas de CTCSS.
    /// </summary>
    public decimal? CtcssFq { get; private set; }

    /// <summary>
    /// Seuil CTCSS (0-100)
    /// </summary>
    public int CtcssThresh { get; private set; } = 15;
}
