using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Régénération de la demande de signature du nœud.
/// </summary>
/// <remarks>
/// Nécessaire quand l'indicatif ou l'identité déclarée changent — le certificat existant
/// porte alors un sujet devenu faux — et après une régénération de PKI côté réflecteur, qui
/// invalide le certificat du nœud sans que celui-ci s'en aperçoive autrement que par une
/// déconnexion sans cause.
/// </remarks>
public interface INodeCertificateRequestResetter
{
    /// <summary>
    /// Supprime la demande et le certificat du nœud pour que SVXLink en produise de nouveaux.
    /// <b>La clé privée est conservée</b> : elle est l'identité du nœud, et rien n'oblige à la
    /// renouveler pour refaire signer une demande.
    /// </summary>
    /// <param name="callsign">Indicatif dont les fichiers sont à effacer.</param>
    /// <returns><c>true</c> si un fichier a été supprimé, <c>false</c> s'il n'y avait rien.</returns>
    Task<Validation<Error, bool>> ResetAsync(string callsign, CancellationToken cancellationToken = default);
}
