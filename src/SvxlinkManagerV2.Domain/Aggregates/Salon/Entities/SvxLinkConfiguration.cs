using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;

/// <summary>
/// Configuration complète SVXLink pour un Salon (Reflector).
/// Représente TOUTES les propriétés des sections GLOBAL, ReflectorLogic et SimplexLogic du fichier svxlink.conf.
/// Utilise un record immutable pour faciliter l'Event Sourcing et la sérialisation.
/// </summary>
public record SvxLinkConfiguration(
    Guid Id,
    // Section GLOBAL
    string Logics,
    string CfgDir,
    int CardSampleRate,
    int CardChannels,
    // Section ReflectorLogic
    string Host,
    int Port,
    string Callsign,
    string? AuthKey,
    int JitterBufferDelay,
    /// <summary>
    /// Protocol version for the reflector connection.
    /// V3 = modern (25.05+, X.509 certificates), V2 = legacy (19.09.2, AUTH_KEY).
    /// </summary>
    ReflectorProtocol ReflectorProtocol,
    /// <summary>
    /// Email address for the X.509 certificate (V3 protocol only). Optional.
    /// </summary>
    string? CertEmail,
    // Section SimplexLogic
    string SimplexCallsign,
    string Modules,
    int ShortIdentInterval,
    int LongIdentInterval,
    string? ReportCtcss,
    string DefaultLang,
    int RgrSoundDelay,
    // Configuration Radio (directement dans Salon, plus de RadioProfil)
    /// <summary>
    /// Fréquence de réception en MHz (format: 145.550). Plage valide: 30-3000 MHz.
    /// </summary>
    decimal RxFrequency,
    /// <summary>
    /// Fréquence de transmission en MHz (format: 145.550). Plage valide: 30-3000 MHz.
    /// </summary>
    decimal TxFrequency,
    /// <summary>
    /// Tonalité CTCSS de réception en Hz (format: 136.5). Plage valide: 67.0-250.3 Hz. Null = aucun CTCSS.
    /// </summary>
    decimal? RxCtcss,
    /// <summary>
    /// Tonalité CTCSS de transmission en Hz (format: 136.5). Plage valide: 67.0-250.3 Hz. Null = aucun CTCSS.
    /// </summary>
    decimal? TxCtcss,
    // Section ReflectorLogic (SVXLink 25.05+ / protocole V3)
    int DefaultTg = 0,
    string? MonitorTgs = null,
    int TgSelectTimeout = 30,
    int? TgSelectInhibitTimeout = null,
    bool MuteFirstTxLoc = true,
    bool MuteFirstTxRem = false,
    int TmpMonitorTimeout = 3600,
    int QsyPendingTimeout = -1,
    /// <summary>
    /// Intervalle des messages de présence UDP, en secondes (<c>UDP_HEARTBEAT_INTERVAL</c>).
    /// La documentation SVXLink en fait le remède aux déconnexions répétées par expiration
    /// de présence : sur une liaison instable — 4G, faisceau radio — c'est le premier
    /// réglage à baisser. 15 est la valeur par défaut de SVXLink, qui n'est alors pas écrite.
    /// </summary>
    int UdpHeartbeatInterval = 15,
    /// <summary>
    /// Intervalle minimal entre deux annonces du même talkgroup activé à distance, en
    /// secondes (<c>ANNOUNCE_REMOTE_MIN_INTERVAL</c>). Sans lui, un talkgroup qui s'active en
    /// boucle fait parler le nœud sans arrêt, au détriment du trafic local. 0 laisse SVXLink
    /// décider et n'écrit rien.
    /// </summary>
    int AnnounceRemoteMinInterval = 0,
    /// <summary>
    /// Journalisation des entrées et sorties du réflecteur (<c>VERBOSE</c>). Les désactiver
    /// est utile sur un réflecteur très fréquenté, où ces lignes noient le reste — le tampon
    /// de logs de l'application plafonne à 1000 lignes. Vrai est la valeur par défaut de
    /// SVXLink, qui n'est alors pas écrite.
    /// </summary>
    bool Verbose = true,
    /// <summary>
    /// Serveurs réflecteur additionnels, <c>hôte[:port]</c> séparés par des virgules
    /// (<c>HOSTS</c>). L'ordre est la priorité : SVXLink tente le serveur principal, puis
    /// chaque suivant. Un port omis reprend celui du serveur principal.
    /// </summary>
    string? AdditionalHosts = null,
    /// <summary>
    /// Domaine de découverte automatique par enregistrements SRV
    /// <c>_svxreflector._tcp.&lt;domaine&gt;</c> (<c>DNS_DOMAIN</c>). Chaque enregistrement
    /// porte hôte, port, priorité et poids : le réseau décide alors de l'ordre, sans
    /// reconfigurer les nœuds.
    /// </summary>
    string? DnsDomain = null,
    // Section ModuleParrot (Parrot salon only)
    /// <summary>
    /// Audio FIFO buffer length in seconds (ModuleParrot). Default: 60.
    /// </summary>
    int ParrotFifoLen = 60,
    /// <summary>
    /// Delay in milliseconds before playback after squelch close (ModuleParrot). Default: 1000.
    /// </summary>
    int ParrotRepeatDelay = 1000,
    /// <summary>
    /// Module inactivity timeout in seconds (ModuleParrot). Default: 180.
    /// </summary>
    int ParrotTimeout = 180
);
