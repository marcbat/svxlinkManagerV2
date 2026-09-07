namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Emplacements de la PKI du nœud, partagés par la génération de <c>svxlink.conf</c>
/// et par la gestion de la confiance envers le réflecteur.
/// </summary>
/// <remarks>
/// Ce répertoire n'est pas propre à une version de SVXLink : les deux installations
/// partagent la même identité de nœud, et seul le protocole V3 s'en sert. Il ne passe
/// donc pas par <c>ISvxLinkVersionStrategy</c>.
/// </remarks>
public static class SvxLinkPkiPaths
{
    /// <summary>Valeur écrite dans <c>CERT_PKI_DIR</c> de la section <c>[ReflectorLogic]</c>.</summary>
    public const string Directory = "/var/lib/svxlink/pki";

    /// <summary>
    /// Bundle des autorités de certification acceptées par le nœud.
    /// Nom par défaut de SVXLink (<c>CERT_CAFILE</c>, non surchargé ici).
    /// </summary>
    public const string CaBundleFile = $"{Directory}/ca-bundle.crt";
}
