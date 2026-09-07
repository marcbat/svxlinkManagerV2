namespace SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Entities;

/// <summary>
/// Nature du nœud, telle que la publie <c>node_info.json</c>.
/// </summary>
/// <remarks>
/// Les libellés sont ceux du modèle de référence de SVXLink et sont écrits tels quels dans
/// le JSON : les traduire les rendrait incompréhensibles aux annuaires qui le lisent.
/// </remarks>
public enum NodeClass
{
    /// <summary>Nœud simplex : émission et réception sur la même fréquence.</summary>
    Simplex,

    /// <summary>Relais : réception et émission sur deux fréquences.</summary>
    Repeater,

    /// <summary>Point d'accès personnel, de faible portée.</summary>
    Hotspot,

    /// <summary>Passerelle vers un autre réseau.</summary>
    Bridge
}

/// <summary>
/// Informations que le nœud publie au réflecteur (<c>NODE_INFO_FILE</c>).
/// </summary>
/// <param name="Location">Ville ou zone couverte (<c>nodeLocation</c>).</param>
/// <param name="Class">Nature du nœud (<c>nodeClass</c>).</param>
/// <param name="Sysop">Indicatifs des responsables, séparés par des espaces (<c>sysop</c>).</param>
/// <param name="Latitude">Latitude en degrés décimaux.</param>
/// <param name="Longitude">Longitude en degrés décimaux.</param>
/// <param name="Locator">Locator Maidenhead, par exemple <c>JN36BX</c>.</param>
/// <param name="Hidden">
/// Le nœud demande à ne pas figurer dans les annuaires. Le fichier est alors publié avec
/// <c>hidden</c> à vrai plutôt que supprimé : c'est ce que le format prévoit, et cela laisse
/// au réflecteur le soin de respecter la demande.
/// </param>
/// <remarks>
/// Ces informations ne se déduisent d'aucune autre configuration — position, classe, sysop —
/// et sont saisies une fois pour toutes. Tout le reste du document publié est construit à
/// partir de ce que l'application connaît déjà du salon actif et de la radio.
/// </remarks>
public record NodeInformation(
    string? Location = null,
    NodeClass Class = NodeClass.Simplex,
    string? Sysop = null,
    double? Latitude = null,
    double? Longitude = null,
    string? Locator = null,
    bool Hidden = false)
{
    /// <summary>Aucune information de nœud renseignée.</summary>
    public static readonly NodeInformation Empty = new();

    /// <summary>Une position est connue, complètement ou par son locator.</summary>
    public bool HasPosition =>
        (Latitude.HasValue && Longitude.HasValue) || !string.IsNullOrWhiteSpace(Locator);

    /// <summary>Libellé attendu par le format de SVXLink pour <c>nodeClass</c>.</summary>
    public string ClassName => Class switch
    {
        NodeClass.Repeater => "repeater",
        NodeClass.Hotspot => "hotspot",
        NodeClass.Bridge => "bridge",
        _ => "simplex"
    };
}
