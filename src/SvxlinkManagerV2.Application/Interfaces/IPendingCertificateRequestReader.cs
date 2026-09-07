using LanguageExt;
using LanguageExt.Common;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Lecture des demandes de signature en attente sur le réflecteur local.
/// </summary>
/// <remarks>
/// Elles sont lues dans <c>&lt;CERT_PKI_DIR&gt;/pending_csrs/</c>, et non par le PTY : la
/// commande <c>CA PENDING</c> de SVXLink 25.05 répond « Not yet implemented ». Le système de
/// fichiers est de toute façon la source la plus directe, le démon y déposant les demandes
/// telles quelles.
/// </remarks>
public interface IPendingCertificateRequestReader
{
    /// <summary>
    /// Demandes en attente, triées par indicatif.
    /// Une liste vide n'est pas une erreur : c'est l'état nominal d'un réflecteur à jour.
    /// </summary>
    Task<Validation<Error, IReadOnlyList<PendingCertificateRequest>>> ListAsync(
        CancellationToken cancellationToken = default);
}
