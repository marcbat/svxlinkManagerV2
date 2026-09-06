namespace SvxlinkManagerV2.Infrastructure.Network.Apt;

/// <summary>
/// Options de la mise à jour applicative fondée sur APT.
/// Remplace la configuration de l'ancien mécanisme GitHub Releases : le dépôt étant
/// public et signé, il n'y a plus ni token ni répertoire de staging à configurer.
/// </summary>
public class AptUpdateOptions
{
    public const string SectionName = "ApplicationUpdate";

    /// <summary>Désactive complètement la consultation des mises à jour.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Nom du paquet Debian à surveiller et à mettre à jour.</summary>
    public string PackageName { get; set; } = "svxlinkmanagerv2";

    /// <summary>Racine du dépôt APT, sans suite ni composant.</summary>
    public string RepositoryUrl { get; set; } = "https://marcbat.github.io/svxlinkManagerV2";

    /// <summary>Architecture Debian déclarée dans la ligne de source.</summary>
    public string Architecture { get; set; } = "armhf";

    /// <summary>Composant du dépôt (unique aujourd'hui).</summary>
    public string Component { get; set; } = "main";

    /// <summary>Fichier de source APT piloté par l'application.</summary>
    public string SourceListPath { get; set; } = "/etc/apt/sources.list.d/svxlinkmanager.list";

    /// <summary>Trousseau contenant la clé publique de signature du dépôt.</summary>
    public string KeyringPath { get; set; } = "/etc/apt/keyrings/svxlinkmanager.gpg";

    /// <summary>
    /// Dépôt GitHub utilisé pour reconstruire l'URL des notes de version.
    /// Le dépôt APT ne transporte pas les notes : seul le numéro de version est connu.
    /// </summary>
    public string ReleaseNotesRepository { get; set; } = "marcbat/svxlinkManagerV2";

    /// <summary>Canal consulté par défaut, au premier démarrage seulement.</summary>
    public string DefaultChannel { get; set; } = "Stable";

    /// <summary>Délai maximal accordé à une commande apt, en secondes.</summary>
    public int CommandTimeoutSeconds { get; set; } = 300;
}
