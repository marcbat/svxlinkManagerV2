using System.Globalization;

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
    /// Préfixe de commande déclaré dans <c>CONNECT_LOGICS</c> pour adresser les commandes
    /// talkgroup à <c>ReflectorLogic</c> (protocole V3 uniquement) — cf. <see cref="DtmfTalkGroupCommands"/>.
    ///
    /// Contrairement aux autres plages, ce n'est pas un intervalle mais un <b>préfixe</b> :
    /// SVXLink route vers la logique liée toute commande qui commence par ces chiffres,
    /// quelle que soit sa longueur. Tout code dont l'écriture décimale commence par « 35 »
    /// est donc indisponible pour un salon (35, 3500-3599 ; 350-359 l'était déjà au titre
    /// de la plage d'annonces).
    ///
    /// Arbitrage (ticket #144) : le préfixe « 9 » des exemples de la documentation SVXLink
    /// a été écarté parce qu'il aurait capté les codes historiques 96 (RRF), 97 (FON) et
    /// 98 (Salon Technique), semés par défaut. « 35 » se loge dans la plage 300-399, déjà
    /// réservée au système, et n'entre en collision avec aucune commande existante :
    /// annonces 301-307, commandes système 310-320, commandes internes 398 et 399.
    /// </summary>
    public const string TalkGroupCommandPrefix = "35";

    /// <summary>
    /// Indique si le code DTMF est dans une plage réservée (modules SVXLink, annonces,
    /// ou préfixe des commandes talkgroup).
    /// </summary>
    public static bool IsReserved(int code)
        => IsInModuleRange(code) || IsInAnnounceRange(code) || IsTalkGroupPrefixed(code);

    /// <summary>
    /// Indique si le code DTMF est valide pour être attribué à un salon.
    /// Valide = dans la plage globale (20-9999), en dehors de la plage d'annonces (300-399)
    /// ET ne commençant pas par le préfixe des commandes talkgroup.
    /// </summary>
    public static bool IsValidForSalon(int code)
        => code >= SalonRangeMin && code <= SalonRangeMax
           && !IsInAnnounceRange(code)
           && !IsTalkGroupPrefixed(code);

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

    /// <summary>
    /// Indique si l'écriture décimale du code commence par <see cref="TalkGroupCommandPrefix"/>,
    /// et serait donc captée par SVXLink avant d'atteindre l'application.
    /// </summary>
    public static bool IsTalkGroupPrefixed(int code)
        => code > 0
           && code.ToString(CultureInfo.InvariantCulture).StartsWith(TalkGroupCommandPrefix, StringComparison.Ordinal);
}
