using System.Security.Cryptography.X509Certificates;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Lit et réinitialise la PKI du nœud, dans <see cref="SvxLinkPkiPaths.Directory"/>.
/// </summary>
/// <remarks>
/// SVXLink nomme ses fichiers d'après l'indicatif : <c>&lt;indicatif&gt;.key</c>,
/// <c>.csr</c> et <c>.crt</c>. L'état se déduit de leur présence, et le certificat lui-même
/// est lu pour son sujet, son émetteur et ses dates.
///
/// <b>La clé privée n'est jamais ouverte.</b> Sa seule présence est constatée : rien de ce
/// que produit ce service ne doit pouvoir la faire remonter jusqu'à une page web.
/// </remarks>
public class NodeCertificateService : INodeCertificateReader, INodeCertificateRequestResetter
{
    private readonly ILogger<NodeCertificateService> _logger;
    private readonly string _pkiDirectory;

    public NodeCertificateService(ILogger<NodeCertificateService> logger, string? pkiDirectory = null)
    {
        _logger = logger;
        _pkiDirectory = pkiDirectory ?? SvxLinkPkiPaths.Directory;
    }

    /// <inheritdoc/>
    public NodeCertificateState Read(string callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign))
            return NodeCertificateState.NotApplicable;

        callsign = callsign.Trim();

        try
        {
            var certificatePath = PathFor(callsign, "crt");
            var requestPath = PathFor(callsign, "csr");

            if (File.Exists(certificatePath))
                return ReadCertificate(callsign, certificatePath);

            // Clé et demande présentes, pas de certificat : le sysop n'a pas encore signé.
            // C'est le cas que l'application montrait comme un simple échec de connexion.
            if (File.Exists(requestPath))
                return new NodeCertificateState(
                    NodeCertificateStatus.PendingSignature,
                    callsign,
                    RequestedAt: File.GetLastWriteTime(requestPath));

            return new NodeCertificateState(NodeCertificateStatus.NotGenerated, callsign);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lecture impossible de la PKI du nœud pour {Callsign}", callsign);
            return new NodeCertificateState(NodeCertificateStatus.Unreadable, callsign);
        }
    }

    private NodeCertificateState ReadCertificate(string callsign, string path)
    {
        try
        {
            // Le fichier écrit par SVXLink contient le certificat du nœud suivi de celui de
            // l'autorité : X509Certificate2 lit le premier, qui est celui du nœud.
            using var certificate = new X509Certificate2(path);

            var status = certificate.NotAfter < DateTime.Now
                ? NodeCertificateStatus.Expired
                : certificate.NotAfter < DateTime.Now.AddDays(NodeCertificateState.ExpiryWarningDays)
                    ? NodeCertificateStatus.Expiring
                    : NodeCertificateStatus.Valid;

            return new NodeCertificateState(
                status,
                callsign,
                certificate.Subject,
                certificate.Issuer,
                certificate.NotBefore,
                certificate.NotAfter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Certificat illisible : {Path}", path);
            return new NodeCertificateState(NodeCertificateStatus.Unreadable, callsign);
        }
    }

    /// <inheritdoc/>
    public Task<Validation<Error, bool>> ResetAsync(
        string callsign,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callsign))
            return Task.FromResult(Validation<Error, bool>.Fail(Seq1(Error.New("Indicatif vide."))));

        callsign = callsign.Trim();

        try
        {
            var removed = false;

            // La clé (.key) est délibérément absente de cette liste : elle est l'identité du
            // nœud, et la renouveler n'apporte rien à une demande à refaire signer.
            foreach (var extension in new[] { "csr", "crt" })
            {
                var path = PathFor(callsign, extension);
                if (!File.Exists(path))
                    continue;

                File.Delete(path);
                removed = true;
                _logger.LogWarning("Fichier de PKI supprimé : {Path}", path);
            }

            return Task.FromResult<Validation<Error, bool>>(removed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de la régénération de la demande de {Callsign}", callsign);
            return Task.FromResult(Validation<Error, bool>.Fail(Seq1(Error.New(ex))));
        }
    }

    private string PathFor(string callsign, string extension) =>
        Path.Combine(_pkiDirectory, $"{callsign}.{extension}");
}
