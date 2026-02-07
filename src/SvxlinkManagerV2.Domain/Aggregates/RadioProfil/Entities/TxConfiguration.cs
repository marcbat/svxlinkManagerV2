using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Entities;

/// <summary>
/// Configuration de transmission pour un RadioProfil.
/// Représente tous les paramètres de la section [Tx1] du fichier svxlink.conf.
/// </summary>
public class TxConfiguration : Entity<Guid>
{
    /// <summary>
    /// Constructeur par défaut requis pour Marten (rehydratation)
    /// </summary>
    public TxConfiguration() : base()
    {
    }

    /// <summary>
    /// Constructeur avec tous les paramètres
    /// </summary>
    public TxConfiguration(
        Guid id,
        string type,
        string audioDev,
        int audioChannel,
        int txDelay,
        int preamp,
        decimal? ctcssFq,
        int ctcssLevel,
        int preemphasis,
        int dtmfToneLength,
        int dtmfToneSpacing,
        int dtmfDigitPwr)
        : base(id)
    {
        Type = type;
        AudioDev = audioDev;
        AudioChannel = audioChannel;
        TxDelay = txDelay;
        Preamp = preamp;
        CtcssFq = ctcssFq;
        CtcssLevel = ctcssLevel;
        Preemphasis = preemphasis;
        DtmfToneLength = dtmfToneLength;
        DtmfToneSpacing = dtmfToneSpacing;
        DtmfDigitPwr = dtmfDigitPwr;
    }

    /// <summary>
    /// Type d'émetteur (ex: "Local")
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
    /// Délai de transmission en millisecondes
    /// </summary>
    public int TxDelay { get; private set; } = 900;

    /// <summary>
    /// Préamplification (-30 à +30 dB)
    /// </summary>
    public int Preamp { get; private set; } = 0;

    /// <summary>
    /// Fréquence CTCSS en Hz (ex: 71.9). Null si pas de CTCSS.
    /// </summary>
    public decimal? CtcssFq { get; private set; }

    /// <summary>
    /// Niveau CTCSS (0-100)
    /// </summary>
    public int CtcssLevel { get; private set; } = 9;

    /// <summary>
    /// Préaccentuation (0-100)
    /// </summary>
    public int Preemphasis { get; private set; } = 0;

    /// <summary>
    /// Durée d'un ton DTMF en millisecondes
    /// </summary>
    public int DtmfToneLength { get; private set; } = 100;

    /// <summary>
    /// Espacement entre tons DTMF en millisecondes
    /// </summary>
    public int DtmfToneSpacing { get; private set; } = 50;

    /// <summary>
    /// Puissance des digits DTMF en dB (-30 à 0)
    /// </summary>
    public int DtmfDigitPwr { get; private set; } = -15;
}
