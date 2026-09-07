namespace SvxlinkManagerV2.Application.Models;

/// <summary>
/// Demande de signature de certificat (CSR) déposée par un nœud et en attente de décision.
/// </summary>
/// <param name="Callsign">
/// Indicatif du nœud demandeur, lu dans le <c>Common Name</c> du sujet de la demande.
/// C'est aussi le nom du fichier, mais le sujet fait foi : le certificat émis portera ce CN.
/// </param>
/// <param name="Email">
/// Adresse déclarée par le nœud (<c>CERT_EMAIL</c>), extraite du Subject Alternative Name.
/// <c>null</c> si le nœud n'en a pas déclaré.
/// </param>
/// <param name="ReceivedAtUtc">Date de dépôt de la demande, d'après le fichier.</param>
/// <param name="FileName">Nom du fichier dans <c>pending_csrs/</c>, pour le diagnostic.</param>
public record PendingCertificateRequest(
    string Callsign,
    string? Email,
    DateTime ReceivedAtUtc,
    string FileName);
