using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Application.Features.Statistics;

/// <summary>
/// Sort réservé à un code DTMF composé.
/// </summary>
public enum DtmfCommandCategory
{
    /// <summary>Code attribué à un salon : le nœud a changé de salon.</summary>
    SalonSwitch = 0,

    /// <summary>Commande système de pilotage du nœud (plage 300-399, cf. <see cref="DtmfSystemCommands"/>).</summary>
    SystemCommand = 1,

    /// <summary>Demande d'annonce vocale (plage 300-399 hors commandes système).</summary>
    Announcement = 2,

    /// <summary>Module SVXLink (plage 1-19) : Perroquet, Aide.</summary>
    SvxLinkModule = 3,

    /// <summary>
    /// Code sans destinataire : hors plages connues, ou dans la plage salon sans salon associé.
    /// Une accumulation de codes inconnus trahit un clavier DTMF mal réglé ou des faux positifs de décodage.
    /// </summary>
    Unknown = 4
}

/// <summary>
/// Classement d'un code DTMF dans sa catégorie et libellé de l'action correspondante.
///
/// La classification est faite **à la lecture**, à partir des salons existants : un code
/// dont le salon a depuis été supprimé ou réattribué apparaîtra donc selon la configuration
/// actuelle, pas selon celle du jour où il a été composé. C'est le prix à payer pour ne rien
/// figer à l'écriture, et cela reste sans conséquence sur les volumes affichés.
/// </summary>
public static class DtmfCommandClassifier
{
    /// <summary>
    /// Classe un code DTMF brut tel qu'il a été reçu.
    /// </summary>
    /// <param name="rawCode">Code composé, éventuellement non numérique.</param>
    /// <param name="salonNamesByDtmfCode">Noms des salons indexés par leur code DTMF.</param>
    public static (DtmfCommandCategory Category, string Label) Classify(
        string rawCode,
        IReadOnlyDictionary<int, string> salonNamesByDtmfCode)
    {
        if (!int.TryParse(rawCode?.Trim(), out var code))
            return (DtmfCommandCategory.Unknown, "Code non numérique");

        if (DtmfCodeRanges.IsInModuleRange(code))
            return (DtmfCommandCategory.SvxLinkModule, $"Module SVXLink {code}");

        if (DtmfSystemCommands.IsSystemCommand(code))
        {
            var system = DtmfSystemCommands.All.First(c => c.Code == code);
            return (DtmfCommandCategory.SystemCommand, system.Description);
        }

        if (DtmfCodeRanges.IsInAnnounceRange(code))
            return (DtmfCommandCategory.Announcement, "Annonce vocale");

        if (salonNamesByDtmfCode.TryGetValue(code, out var salonName))
            return (DtmfCommandCategory.SalonSwitch, $"Salon {salonName}");

        return (DtmfCommandCategory.Unknown, "Aucun salon associé");
    }

    /// <summary>Libellé français d'une catégorie.</summary>
    /// <param name="category">Catégorie à nommer.</param>
    public static string ToLabel(this DtmfCommandCategory category) => category switch
    {
        DtmfCommandCategory.SalonSwitch => "Changement de salon",
        DtmfCommandCategory.SystemCommand => "Commande système",
        DtmfCommandCategory.Announcement => "Annonce vocale",
        DtmfCommandCategory.SvxLinkModule => "Module SVXLink",
        _ => "Sans destinataire"
    };
}
