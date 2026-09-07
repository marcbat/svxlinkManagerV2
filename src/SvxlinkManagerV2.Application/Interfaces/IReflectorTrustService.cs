using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Gestion de la confiance du nœud envers l'autorité de certification du réflecteur
/// (protocole V3).
/// </summary>
/// <remarks>
/// SVXLink mémorise la CA du serveur dans un fichier <c>ca-bundle.crt</c> de son
/// <c>CERT_PKI_DIR</c>, téléchargé au premier contact (<c>CERT_DOWNLOAD_CA_BUNDLE</c>, actif
/// par défaut). Si le réflecteur régénère sa PKI, ce fichier devient périmé : le nœud refuse
/// la nouvelle chaîne et la liaison échoue tant que le fichier n'a pas été supprimé.
/// </remarks>
public interface IReflectorTrustService
{
    /// <summary>
    /// Supprime le bundle CA mémorisé, pour que le nœud retélécharge celui du serveur.
    /// </summary>
    /// <returns>
    /// <c>true</c> si un bundle a effectivement été supprimé, <c>false</c> s'il n'y en avait
    /// pas — l'opération est idempotente et cette absence n'est pas une erreur.
    /// </returns>
    Task<Validation<Error, bool>> ResetTrustAsync(CancellationToken cancellationToken = default);
}
