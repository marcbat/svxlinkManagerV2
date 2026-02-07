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
    string AuthKey,
    string AudioCodec,
    int JitterBufferDelay,
    // Section SimplexLogic
    string SimplexCallsign,
    string Modules,
    int ShortIdentInterval,
    int LongIdentInterval,
    string? ReportCtcss,
    string EventHandler,
    string DefaultLang,
    int RgrSoundDelay,
    // Références vers autres Aggregates
    Guid? SoundId,
    Guid RadioProfilId
);
