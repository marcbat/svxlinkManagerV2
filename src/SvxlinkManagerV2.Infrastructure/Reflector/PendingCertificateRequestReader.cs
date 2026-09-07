using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Reflector;

/// <summary>
/// Implémentation de <see cref="IPendingCertificateRequestReader"/> : lit les demandes
/// déposées dans <c>&lt;CERT_PKI_DIR&gt;/pending_csrs/</c>.
/// </summary>
/// <remarks>
/// L'analyse est faite par .NET (<see cref="CertificateRequest.LoadSigningRequestPem(string,
/// HashAlgorithmName, CertificateRequestLoadOptions, RSASignaturePadding?)"/>) et non par le
/// binaire <c>openssl</c> : le seul composant du projet qui en dépend est le hook de
/// signature du développement, et aucune dépendance externe n'a besoin d'être ajoutée à la
/// cible de production pour lire une demande.
/// </remarks>
public class PendingCertificateRequestReader : IPendingCertificateRequestReader
{
    /// <summary>Extension des demandes déposées par le démon.</summary>
    private const string CsrExtension = "*.csr";

    /// <summary>OID du Subject Alternative Name, qui porte l'adresse déclarée par le nœud.</summary>
    private const string SubjectAlternativeNameOid = "2.5.29.17";

    private readonly ILogger<PendingCertificateRequestReader> _logger;
    private readonly ReflectorConfigurationFile _configuration;

    public PendingCertificateRequestReader(
        ILogger<PendingCertificateRequestReader> logger,
        string? configPath = null)
    {
        _logger = logger;
        _configuration = new ReflectorConfigurationFile(configPath);
    }

    /// <inheritdoc/>
    public Task<Validation<Error, IReadOnlyList<PendingCertificateRequest>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = _configuration.ReadPendingCsrsDirectory();
            if (directory is null)
                return Task.FromResult(Fail(
                    $"La configuration du réflecteur ne déclare pas {ReflectorConfigurationFile.PkiDirectoryKey} " +
                    "dans [GLOBAL] : les demandes en attente sont introuvables."));

            // Le répertoire n'est créé qu'à la première demande : son absence signifie
            // « aucune demande », pas une panne.
            if (!Directory.Exists(directory))
                return Task.FromResult(Success([]));

            var requests = Directory
                .EnumerateFiles(directory, CsrExtension)
                .Select(ReadRequest)
                .OfType<PendingCertificateRequest>()
                .OrderBy(request => request.Callsign, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Task.FromResult(Success(requests));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la lecture des demandes de certificat en attente");
            return Task.FromResult(Validation<Error, IReadOnlyList<PendingCertificateRequest>>.Fail(
                Seq1(Error.New(ex))));
        }
    }

    /// <summary>
    /// Interprète une demande. Retourne <c>null</c> si le fichier est illisible : une demande
    /// corrompue ne doit pas faire disparaître les autres de la liste.
    /// </summary>
    private PendingCertificateRequest? ReadRequest(string path)
    {
        try
        {
            var csr = CertificateRequest.LoadSigningRequestPem(
                File.ReadAllText(path),
                HashAlgorithmName.SHA256,
                // Les extensions de la demande portent l'adresse du nœud. Elles sont
                // « unsafe » au sens où elles ne sont pas vérifiées — ce qui est précisément
                // la question posée à l'opérateur, et la raison pour laquelle il décide.
                CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);

            var callsign = ReadCommonName(csr.SubjectName);
            if (string.IsNullOrWhiteSpace(callsign))
            {
                _logger.LogWarning("Demande de certificat sans Common Name ignorée : {Path}", path);
                return null;
            }

            return new PendingCertificateRequest(
                callsign,
                ReadEmail(csr),
                File.GetLastWriteTimeUtc(path),
                Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Demande de certificat illisible ignorée : {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// Common Name du sujet : c'est l'indicatif que portera le certificat émis.
    /// </summary>
    /// <remarks>
    /// Lu dans le sujet et non déduit du nom de fichier : c'est le sujet qui sera signé, et
    /// les deux pourraient diverger.
    /// </remarks>
    private static string? ReadCommonName(X500DistinguishedName subject) =>
        subject.EnumerateRelativeDistinguishedNames()
            .Where(rdn => rdn.GetSingleElementType().Value == "2.5.4.3")
            .Select(rdn => rdn.GetSingleElementValue())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    /// <summary>Adresse déclarée par le nœud dans son Subject Alternative Name.</summary>
    /// <remarks>
    /// Le SAN est décodé en ASN.1 plutôt que par <c>X509SubjectAlternativeNameExtension</c> :
    /// son énumération des adresses n'existe qu'à partir de .NET 9, et les projets sources
    /// ciblent net8.0. Se rabattre sur <c>Format()</c> serait pire — sa sortie est traduite
    /// dans la langue du système.
    ///
    /// <c>GeneralNames ::= SEQUENCE OF GeneralName</c>, et <c>rfc822Name</c> y est la
    /// variante de contexte [1], une IA5String.
    /// </remarks>
    private static string? ReadEmail(CertificateRequest csr)
    {
        foreach (var extension in csr.CertificateExtensions)
        {
            if (extension.Oid?.Value != SubjectAlternativeNameOid)
                continue;

            var email = ReadFirstRfc822Name(extension.RawData);
            if (!string.IsNullOrWhiteSpace(email))
                return email;
        }

        return null;
    }

    private static string? ReadFirstRfc822Name(byte[] rawData)
    {
        var names = new AsnReader(rawData, AsnEncodingRules.DER).ReadSequence();

        while (names.HasData)
        {
            var tag = names.PeekTag();

            if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 1)
                return names.ReadCharacterString(UniversalTagNumber.IA5String, tag);

            names.ReadEncodedValue();
        }

        return null;
    }

    private static Validation<Error, IReadOnlyList<PendingCertificateRequest>> Success(
        IReadOnlyList<PendingCertificateRequest> requests) =>
        Validation<Error, IReadOnlyList<PendingCertificateRequest>>.Success(requests);

    private Validation<Error, IReadOnlyList<PendingCertificateRequest>> Fail(string message)
    {
        _logger.LogWarning(message);
        return Validation<Error, IReadOnlyList<PendingCertificateRequest>>.Fail(Seq1(Error.New(message)));
    }
}
