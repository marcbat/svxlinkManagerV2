using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Lecture de l'état du certificat X.509 du nœud, dans sa PKI locale.
/// </summary>
/// <remarks>
/// En protocole V3, obtenir un certificat est un processus asynchrone qui fait intervenir un
/// tiers : le nœud génère sa clé et sa demande, l'envoie au réflecteur, puis attend que le
/// sysop la signe. Sans cette lecture, l'application ne montre de cette attente qu'un « échec
/// de connexion » — et un opérateur en conclut, à raison de son point de vue, que le logiciel
/// ne fonctionne pas.
/// </remarks>
public interface INodeCertificateReader
{
    /// <summary>
    /// État du certificat pour l'indicatif donné.
    /// </summary>
    /// <param name="callsign">Indicatif du salon actif, qui nomme les fichiers de la PKI.</param>
    NodeCertificateState Read(string callsign);
}
