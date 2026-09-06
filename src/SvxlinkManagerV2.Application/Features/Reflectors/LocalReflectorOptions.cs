namespace SvxlinkManagerV2.Application.Features.Reflectors;

/// <summary>
/// Adresse du réflecteur local, section <c>LocalReflector</c> des appsettings.
/// </summary>
/// <remarks>
/// Le salon « Réflecteur Local » seedé pointe sur cette adresse. Elle ne peut pas être
/// codée en dur : sur un nœud de production le démon <c>svxreflector</c> tourne sur la
/// machine elle-même (l'adresse de bouclage convient), mais dans la stack Docker il vit
/// dans un autre conteneur, où <c>127.0.0.1</c> ne désigne rien — le seul salon V3 livré
/// par défaut était donc inutilisable sans édition manuelle.
/// </remarks>
public class LocalReflectorOptions
{
    /// <summary>Nom de la section de configuration.</summary>
    public const string SectionName = "LocalReflector";

    /// <summary>
    /// Hôte du réflecteur local. Valeur de production par défaut : le démon tourne
    /// sur le nœud lui-même. La stack Docker la surcharge avec le nom du service.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>Port TCP du réflecteur local (<c>LISTEN_PORT</c> de svxreflector.conf).</summary>
    public int Port { get; set; } = 5300;
}
