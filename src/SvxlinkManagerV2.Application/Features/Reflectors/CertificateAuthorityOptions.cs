namespace SvxlinkManagerV2.Application.Features.Reflectors;

/// <summary>
/// Autorité de certification du réflecteur local, section <c>ReflectorCertificateAuthority</c>
/// des appsettings.
/// </summary>
/// <remarks>
/// L'auto-signature n'est délibérément pas une case à cocher de l'interface : elle autorise
/// <b>n'importe quel</b> indicatif à se connecter au réflecteur, et sur un nœud joignable
/// depuis l'extérieur cela revient à l'ouvrir à tout venant. La friction d'un fichier de
/// configuration à éditer est exactement celle que mérite ce réglage.
/// </remarks>
public class CertificateAuthorityOptions
{
    /// <summary>Nom de la section de configuration.</summary>
    public const string SectionName = "ReflectorCertificateAuthority";

    /// <summary>
    /// Signer automatiquement toute demande en attente. <b>Réservé au développement.</b>
    /// Faux par défaut : sur un réflecteur exploité, la signature est une décision humaine.
    /// </summary>
    public bool AutoSign { get; set; }

    /// <summary>
    /// Cadence de recherche des demandes à signer automatiquement, en secondes.
    /// Sans effet quand <see cref="AutoSign"/> est faux.
    /// </summary>
    public int AutoSignIntervalSeconds { get; set; } = 10;
}
