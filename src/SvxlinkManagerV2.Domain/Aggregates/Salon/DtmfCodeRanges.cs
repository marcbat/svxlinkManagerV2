namespace SvxlinkManagerV2.Domain.Aggregates.Salon;

/// <summary>
/// Définit les plages de codes DTMF réservées et valides pour le système.
/// Les mêmes bornes doivent être synchronisées dans Logic.tcl (script TCL embarqué).
/// </summary>
public static class DtmfCodeRanges
{
    /// <summary>Borne inférieure de la plage réservée aux modules SVXLink (Parrot, Help, etc.).</summary>
    public const int ModuleRangeMin = 1;

    /// <summary>Borne supérieure de la plage réservée aux modules SVXLink.</summary>
    public const int ModuleRangeMax = 19;

    /// <summary>Borne inférieure de la plage des codes salon attribuables.</summary>
    public const int SalonRangeMin = 20;

    /// <summary>Borne supérieure globale des codes DTMF.</summary>
    public const int SalonRangeMax = 9999;

    /// <summary>Borne inférieure de la plage réservée aux commandes d'annonce vocale.</summary>
    public const int AnnounceRangeMin = 300;

    /// <summary>Borne supérieure de la plage réservée aux commandes d'annonce vocale.</summary>
    public const int AnnounceRangeMax = 399;

    /// <summary>
    /// Indique si le code DTMF est dans une plage réservée (modules SVXLink ou annonces).
    /// </summary>
    public static bool IsReserved(int code)
        => IsInModuleRange(code) || IsInAnnounceRange(code);

    /// <summary>
    /// Indique si le code DTMF est valide pour être attribué à un salon.
    /// Valide = dans la plage globale (20-9999) ET en dehors de la plage d'annonces (300-399).
    /// </summary>
    public static bool IsValidForSalon(int code)
        => code >= SalonRangeMin && code <= SalonRangeMax && !IsInAnnounceRange(code);

    /// <summary>
    /// Indique si le code DTMF est dans la plage des modules SVXLink (1-19).
    /// </summary>
    public static bool IsInModuleRange(int code)
        => code >= ModuleRangeMin && code <= ModuleRangeMax;

    /// <summary>
    /// Indique si le code DTMF est dans la plage des commandes d'annonce (300-399).
    /// </summary>
    public static bool IsInAnnounceRange(int code)
        => code >= AnnounceRangeMin && code <= AnnounceRangeMax;
}
