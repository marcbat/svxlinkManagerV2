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
    decimal? TxCtcss
);
