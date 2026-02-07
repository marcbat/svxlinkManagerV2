using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;

/// <summary>
/// Configuration complète SVXLink pour un Salon (Reflector).
/// Représente TOUTES les propriétés des sections GLOBAL, ReflectorLogic et SimplexLogic du fichier svxlink.conf.
/// </summary>
public class SvxLinkConfiguration : Entity<Guid>
{
    /// <summary>
    /// Constructeur par défaut requis pour Marten (rehydratation)
    /// </summary>
    public SvxLinkConfiguration() : base()
    {
    }

    /// <summary>
    /// Constructeur avec tous les paramètres
    /// </summary>
    public SvxLinkConfiguration(
        Guid id,
        // Section GLOBAL
        string logics,
        string cfgDir,
        int cardSampleRate,
        int cardChannels,
        // Section ReflectorLogic
        string host,
        int port,
        string callsign,
        string authKey,
        string audioCodec,
        int jitterBufferDelay,
        // Section SimplexLogic
        string simplexCallsign,
        string modules,
        int shortIdentInterval,
        int longIdentInterval,
        string? reportCtcss,
        string eventHandler,
        string defaultLang,
        int rgrSoundDelay,
        // Références vers autres Aggregates
        Guid? soundId,
        Guid radioProfilId)
        : base(id)
    {
        // Section GLOBAL
        Logics = logics;
        CfgDir = cfgDir;
        CardSampleRate = cardSampleRate;
        CardChannels = cardChannels;

        // Section ReflectorLogic
        Host = host;
        Port = port;
        Callsign = callsign;
        AuthKey = authKey;
        AudioCodec = audioCodec;
        JitterBufferDelay = jitterBufferDelay;

        // Section SimplexLogic
        SimplexCallsign = simplexCallsign;
        Modules = modules;
        ShortIdentInterval = shortIdentInterval;
        LongIdentInterval = longIdentInterval;
        ReportCtcss = reportCtcss;
        EventHandler = eventHandler;
        DefaultLang = defaultLang;
        RgrSoundDelay = rgrSoundDelay;

        // Références
        SoundId = soundId;
        RadioProfilId = radioProfilId;
    }

    #region Section GLOBAL

    /// <summary>
    /// Liste des logiques activées (ex: "SimplexLogic,ReflectorLogic")
    /// </summary>
    public string Logics { get; private set; } = "SimplexLogic,ReflectorLogic";

    /// <summary>
    /// Répertoire de configuration supplémentaire (ex: "svxlink.d")
    /// </summary>
    public string CfgDir { get; private set; } = "svxlink.d";

    /// <summary>
    /// Taux d'échantillonnage de la carte son (ex: 16000 Hz)
    /// </summary>
    public int CardSampleRate { get; private set; } = 16000;

    /// <summary>
    /// Nombre de canaux de la carte son (1 = mono, 2 = stereo)
    /// </summary>
    public int CardChannels { get; private set; } = 1;

    #endregion

    #region Section ReflectorLogic

    /// <summary>
    /// Hôte du reflector SVXLink (ex: "ref.f5kri.fr")
    /// </summary>
    public string Host { get; private set; } = string.Empty;

    /// <summary>
    /// Port TCP du reflector (défaut: 5300)
    /// </summary>
    public int Port { get; private set; } = 5300;

    /// <summary>
    /// Indicatif du nœud (ex: "F5ABC-L")
    /// </summary>
    public string Callsign { get; private set; } = string.Empty;

    /// <summary>
    /// Clé d'authentification pour le reflector
    /// </summary>
    public string AuthKey { get; private set; } = string.Empty;

    /// <summary>
    /// Codec audio utilisé (ex: "OPUS", "GSM", "SPEEX")
    /// </summary>
    public string AudioCodec { get; private set; } = "OPUS";

    /// <summary>
    /// Délai du buffer de gigue en millisecondes (0 = automatique)
    /// </summary>
    public int JitterBufferDelay { get; private set; } = 0;

    #endregion

    #region Section SimplexLogic

    /// <summary>
    /// Indicatif pour la logique simplex (ex: "F5ABC")
    /// </summary>
    public string SimplexCallsign { get; private set; } = string.Empty;

    /// <summary>
    /// Liste des modules activés (ex: "ModuleHelp,ModuleParrot,ModuleEchoLink")
    /// </summary>
    public string Modules { get; private set; } = "ModuleHelp,ModuleParrot";

    /// <summary>
    /// Intervalle d'identification courte en secondes (ex: 60)
    /// </summary>
    public int ShortIdentInterval { get; private set; } = 60;

    /// <summary>
    /// Intervalle d'identification longue en secondes (ex: 60)
    /// </summary>
    public int LongIdentInterval { get; private set; } = 60;

    /// <summary>
    /// Code CTCSS à rapporter (provient du RadioProfil, peut être null)
    /// </summary>
    public string? ReportCtcss { get; private set; }

    /// <summary>
    /// Chemin vers le script de gestion des événements TCL
    /// </summary>
    public string EventHandler { get; private set; } = "/usr/share/svxlink/events.tcl";

    /// <summary>
    /// Langue par défaut (ex: "fr_FR", "en_US")
    /// </summary>
    public string DefaultLang { get; private set; } = "fr_FR";

    /// <summary>
    /// Délai du son RGR en millisecondes
    /// </summary>
    public int RgrSoundDelay { get; private set; } = 0;

    #endregion

    #region Références vers autres Aggregates

    /// <summary>
    /// Identifiant du Sound (annonce vocale) utilisé par le Salon (optionnel)
    /// </summary>
    public Guid? SoundId { get; private set; }

    /// <summary>
    /// Identifiant du RadioProfil (configuration Rx/Tx) utilisé par le Salon (obligatoire)
    /// </summary>
    public Guid RadioProfilId { get; private set; }

    #endregion

    /// <summary>
    /// Met à jour la configuration complète
    /// </summary>
    internal void Update(
        string? logics = null,
        string? cfgDir = null,
        int? cardSampleRate = null,
        int? cardChannels = null,
        string? host = null,
        int? port = null,
        string? callsign = null,
        string? authKey = null,
        string? audioCodec = null,
        int? jitterBufferDelay = null,
        string? simplexCallsign = null,
        string? modules = null,
        int? shortIdentInterval = null,
        int? longIdentInterval = null,
        string? reportCtcss = null,
        string? eventHandler = null,
        string? defaultLang = null,
        int? rgrSoundDelay = null,
        Guid? soundId = null,
        Guid? radioProfilId = null)
    {
        // Section GLOBAL
        if (logics != null) Logics = logics;
        if (cfgDir != null) CfgDir = cfgDir;
        if (cardSampleRate.HasValue) CardSampleRate = cardSampleRate.Value;
        if (cardChannels.HasValue) CardChannels = cardChannels.Value;

        // Section ReflectorLogic
        if (host != null) Host = host;
        if (port.HasValue) Port = port.Value;
        if (callsign != null) Callsign = callsign;
        if (authKey != null) AuthKey = authKey;
        if (audioCodec != null) AudioCodec = audioCodec;
        if (jitterBufferDelay.HasValue) JitterBufferDelay = jitterBufferDelay.Value;

        // Section SimplexLogic
        if (simplexCallsign != null) SimplexCallsign = simplexCallsign;
        if (modules != null) Modules = modules;
        if (shortIdentInterval.HasValue) ShortIdentInterval = shortIdentInterval.Value;
        if (longIdentInterval.HasValue) LongIdentInterval = longIdentInterval.Value;
        if (reportCtcss != null) ReportCtcss = reportCtcss;
        if (eventHandler != null) EventHandler = eventHandler;
        if (defaultLang != null) DefaultLang = defaultLang;
        if (rgrSoundDelay.HasValue) RgrSoundDelay = rgrSoundDelay.Value;

        // Références
        if (soundId.HasValue) SoundId = soundId.Value;
        if (radioProfilId.HasValue) RadioProfilId = radioProfilId.Value;
    }
}
