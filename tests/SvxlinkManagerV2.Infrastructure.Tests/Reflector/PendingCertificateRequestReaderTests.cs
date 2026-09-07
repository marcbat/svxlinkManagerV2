using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.Reflector;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Reflector;

/// <summary>
/// Tests de la lecture des demandes de signature déposées par le démon.
/// </summary>
/// <remarks>
/// Les demandes sont fabriquées avec l'API .NET plutôt que collées en dur : c'est la même
/// API qui les relit, et un PEM figé finirait par mentir sur ce que produit réellement un
/// nœud. La forme visée est celle relevée sur la stack le 07/09/2026 — un sujet réduit au
/// Common Name, et l'adresse du nœud dans le Subject Alternative Name.
/// </remarks>
public class PendingCertificateRequestReaderTests : IDisposable
{
    private readonly ILogger<PendingCertificateRequestReader> _logger =
        Substitute.For<ILogger<PendingCertificateRequestReader>>();

    private readonly string _root;
    private readonly string _configPath;

    public PendingCertificateRequestReaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"svxreflector-pki-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _configPath = Path.Combine(_root, "svxreflector.conf");
        WriteConfig($"[GLOBAL]\nCERT_PKI_DIR={_root.Replace('\\', '/')}\n");
    }

    private void WriteConfig(string content) => File.WriteAllText(_configPath, content);

    private string PendingDirectory => Path.Combine(_root, "pending_csrs");

    private PendingCertificateRequestReader CreateReader() => new(_logger, _configPath);

    /// <summary>Dépose une demande comme le ferait le démon.</summary>
    private void WriteRequest(string callsign, string? email = null, string? fileName = null)
    {
        Directory.CreateDirectory(PendingDirectory);

        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={callsign}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        if (email is not null)
        {
            var builder = new SubjectAlternativeNameBuilder();
            builder.AddEmailAddress(email);
            request.CertificateExtensions.Add(builder.Build());
        }

        File.WriteAllText(
            Path.Combine(PendingDirectory, fileName ?? $"{callsign}.csr"),
            request.CreateSigningRequestPem());
    }

    [Fact]
    public async Task ListAsync_ShouldReadTheCallsignAndTheDeclaredEmail()
    {
        WriteRequest("HB9GXP3-H", "node3@svxlink.test");

        var result = await CreateReader().ListAsync();

        result.ShouldBeSuccess(requests =>
        {
            var request = requests.Should().ContainSingle().Subject;
            request.Callsign.Should().Be("HB9GXP3-H");
            request.Email.Should().Be("node3@svxlink.test");
            request.FileName.Should().Be("HB9GXP3-H.csr");
        });
    }

    [Fact]
    public async Task ListAsync_WithoutAnyEmail_ShouldStillListTheRequest()
    {
        // CERT_EMAIL est facultatif côté nœud : son absence ne doit pas cacher la demande.
        WriteRequest("HB9GXP2-H");

        var result = await CreateReader().ListAsync();

        result.ShouldBeSuccess(requests =>
            requests.Should().ContainSingle().Which.Email.Should().BeNull());
    }

    [Fact]
    public async Task ListAsync_ShouldOrderRequestsByCallsign()
    {
        WriteRequest("HB9ZZZ-H");
        WriteRequest("HB9AAA-H");

        var result = await CreateReader().ListAsync();

        result.ShouldBeSuccess(requests =>
            requests.Select(r => r.Callsign).Should().Equal("HB9AAA-H", "HB9ZZZ-H"));
    }

    /// <summary>
    /// Le nom de fichier n'est pas la source : c'est le sujet qui sera signé, et les deux
    /// pourraient diverger. Signer d'après le nom de fichier émettrait un certificat pour un
    /// autre indicatif que celui annoncé à l'opérateur.
    /// </summary>
    [Fact]
    public async Task ListAsync_ShouldTrustTheSubjectRatherThanTheFileName()
    {
        WriteRequest("HB9REAL-H", fileName: "autre-chose.csr");

        var result = await CreateReader().ListAsync();

        result.ShouldBeSuccess(requests =>
        {
            requests.Should().ContainSingle().Which.Callsign.Should().Be("HB9REAL-H");
            requests[0].FileName.Should().Be("autre-chose.csr");
        });
    }

    [Fact]
    public async Task ListAsync_WithACorruptRequest_ShouldKeepTheOthers()
    {
        // Une demande illisible ne doit pas faire disparaître la liste entière.
        WriteRequest("HB9GXP3-H");
        Directory.CreateDirectory(PendingDirectory);
        File.WriteAllText(Path.Combine(PendingDirectory, "cassee.csr"), "ceci n'est pas un CSR");

        var result = await CreateReader().ListAsync();

        result.ShouldBeSuccess(requests =>
            requests.Should().ContainSingle().Which.Callsign.Should().Be("HB9GXP3-H"));
    }

    [Fact]
    public async Task ListAsync_WithoutAnyPendingDirectory_ShouldReturnAnEmptyList()
    {
        // Le répertoire n'est créé qu'à la première demande : son absence est l'état nominal
        // d'un réflecteur à jour, pas une panne.
        var result = await CreateReader().ListAsync();

        result.ShouldBeSuccess(requests => requests.Should().BeEmpty());
    }

    [Fact]
    public async Task ListAsync_WithoutPkiDirectoryInTheConfiguration_ShouldFail()
    {
        WriteConfig("[GLOBAL]\nLISTEN_PORT=5300\n");

        var result = await CreateReader().ListAsync();

        result.ShouldBeFail(errors => errors.Head.Message.Should().Contain("CERT_PKI_DIR"));
    }

    [Fact]
    public async Task ListAsync_ShouldIgnoreFilesThatAreNotRequests()
    {
        WriteRequest("HB9GXP3-H");
        File.WriteAllText(Path.Combine(PendingDirectory, "notes.txt"), "sans rapport");

        var result = await CreateReader().ListAsync();

        result.ShouldBeSuccess(requests => requests.Should().ContainSingle());
    }

    /// <summary>
    /// Demande produite par SVXLink 25.05 lui-même, capturée sur la stack le 07/09/2026.
    /// </summary>
    /// <remarks>
    /// Les autres tests fabriquent leurs demandes avec l'API .NET : ils prouvent que le
    /// lecteur sait relire ce que .NET écrit, pas ce que SVXLink écrit. Cette demande-ci est
    /// la seule preuve que la forme réelle — sujet réduit au Common Name, adresse dans le
    /// Subject Alternative Name — reste lisible.
    /// </remarks>
    [Fact]
    public async Task ListAsync_ShouldReadARequestProducedBySvxLinkItself()
    {
        Directory.CreateDirectory(PendingDirectory);
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Reflector", "Fixtures", "HB9GXP3-H.csr"),
            Path.Combine(PendingDirectory, "HB9GXP3-H.csr"));

        var result = await CreateReader().ListAsync();

        result.ShouldBeSuccess(requests =>
        {
            var request = requests.Should().ContainSingle().Subject;
            request.Callsign.Should().Be("HB9GXP3-H");
            request.Email.Should().Be("node3@svxlink.test");
        });
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* rien à nettoyer */ }
    }
}
