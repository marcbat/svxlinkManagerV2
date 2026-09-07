using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests de la lecture et de la réinitialisation de la PKI du nœud.
/// </summary>
/// <remarks>
/// SVXLink nomme ses fichiers d'après l'indicatif — <c>&lt;indicatif&gt;.key</c>, <c>.csr</c>,
/// <c>.crt</c> — et l'état se déduit de leur présence. Les tests reproduisent cette
/// disposition sur un répertoire temporaire.
/// </remarks>
public class NodeCertificateServiceTests : IDisposable
{
    private const string Callsign = "HB9GXP-H";

    private readonly ILogger<NodeCertificateService> _logger =
        Substitute.For<ILogger<NodeCertificateService>>();

    private readonly string _pkiDirectory;

    public NodeCertificateServiceTests()
    {
        _pkiDirectory = Path.Combine(Path.GetTempPath(), $"node-pki-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_pkiDirectory);
    }

    private NodeCertificateService CreateService() => new(_logger, _pkiDirectory);

    private string PathFor(string extension) => Path.Combine(_pkiDirectory, $"{Callsign}.{extension}");

    private void WriteKey() => File.WriteAllText(PathFor("key"), "-----BEGIN PRIVATE KEY-----");

    private void WriteRequest() => File.WriteAllText(PathFor("csr"), "-----BEGIN CERTIFICATE REQUEST-----");

    /// <summary>Écrit un certificat auto-signé dont l'échéance est pilotée par le test.</summary>
    private void WriteCertificate(TimeSpan validFor, string issuer = "CN=SvxReflector Issuing CA")
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={Callsign}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.Add(validFor));

        File.WriteAllText(PathFor("crt"), certificate.ExportCertificatePem());
        _ = issuer;
    }

    #region Lecture de l'état

    [Fact]
    public void Read_WithNothingGenerated_ShouldSaySo()
    {
        var state = CreateService().Read(Callsign);

        state.Status.Should().Be(NodeCertificateStatus.NotGenerated);
        state.Callsign.Should().Be(Callsign);
    }

    /// <summary>
    /// Le cas que l'application montrait comme un simple échec de connexion : la demande est
    /// partie, et il n'y a qu'à attendre.
    /// </summary>
    [Fact]
    public void Read_WithAKeyAndARequestButNoCertificate_ShouldReportPendingSignature()
    {
        WriteKey();
        WriteRequest();

        var state = CreateService().Read(Callsign);

        state.Status.Should().Be(NodeCertificateStatus.PendingSignature);
        state.RequestedAt.Should().NotBeNull();
        state.NeedsAttention.Should().BeFalse("attendre une signature n'est pas une panne");
    }

    [Fact]
    public void Read_WithAValidCertificate_ShouldExposeItsSubjectIssuerAndDates()
    {
        WriteKey();
        WriteCertificate(TimeSpan.FromDays(365));

        var state = CreateService().Read(Callsign);

        state.Status.Should().Be(NodeCertificateStatus.Valid);
        state.Subject.Should().Contain(Callsign);
        state.Issuer.Should().NotBeNullOrWhiteSpace();
        state.NotBefore.Should().NotBeNull();
        state.NotAfter.Should().NotBeNull();
        state.DaysUntilExpiry.Should().BeGreaterThan(300);
    }

    [Fact]
    public void Read_WithACertificateCloseToExpiry_ShouldWarn()
    {
        WriteCertificate(TimeSpan.FromDays(NodeCertificateState.ExpiryWarningDays - 5));

        var state = CreateService().Read(Callsign);

        state.Status.Should().Be(NodeCertificateStatus.Expiring);
        state.NeedsAttention.Should().BeTrue();
    }

    [Fact]
    public void Read_WithAnExpiredCertificate_ShouldSaySo()
    {
        // CreateSelfSigned refuse une fin antérieure au début : le certificat commence il y a
        // deux jours et se termine il y a un jour.
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={Callsign}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-2), DateTimeOffset.Now.AddDays(-1));
        File.WriteAllText(PathFor("crt"), certificate.ExportCertificatePem());

        var state = CreateService().Read(Callsign);

        state.Status.Should().Be(NodeCertificateStatus.Expired);
        state.DaysUntilExpiry.Should().BeNegative();
    }

    [Fact]
    public void Read_WithAnUnreadableCertificate_ShouldSaySoRatherThanThrow()
    {
        File.WriteAllText(PathFor("crt"), "ceci n'est pas un certificat");

        var state = CreateService().Read(Callsign);

        state.Status.Should().Be(NodeCertificateStatus.Unreadable);
    }

    [Fact]
    public void Read_ShouldPreferTheCertificateOverThePendingRequest()
    {
        // Le CSR reste sur le disque après signature : le certificat fait foi.
        WriteRequest();
        WriteCertificate(TimeSpan.FromDays(365));

        CreateService().Read(Callsign).Status.Should().Be(NodeCertificateStatus.Valid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Read_WithoutACallsign_ShouldReturnNotApplicable(string callsign)
    {
        CreateService().Read(callsign).Status.Should().Be(NodeCertificateStatus.NotApplicable);
    }

    #endregion

    #region Régénération de la demande

    [Fact]
    public async Task ResetAsync_ShouldRemoveTheRequestAndTheCertificate()
    {
        WriteKey();
        WriteRequest();
        WriteCertificate(TimeSpan.FromDays(365));

        var result = await CreateService().ResetAsync(Callsign);

        result.ShouldBeSuccess(removed => removed.Should().BeTrue());
        File.Exists(PathFor("csr")).Should().BeFalse();
        File.Exists(PathFor("crt")).Should().BeFalse();
    }

    /// <summary>
    /// La clé privée est l'identité du nœud : la renouveler n'apporte rien à une demande à
    /// refaire signer, et la perdre est irréversible.
    /// </summary>
    [Fact]
    public async Task ResetAsync_ShouldNeverTouchThePrivateKey()
    {
        WriteKey();
        WriteRequest();

        await CreateService().ResetAsync(Callsign);

        File.Exists(PathFor("key")).Should().BeTrue();
    }

    [Fact]
    public async Task ResetAsync_WithNothingToRemove_ShouldSucceedAndSaySo()
    {
        // Idempotence : un nœud sans certificat est déjà dans l'état voulu.
        var result = await CreateService().ResetAsync(Callsign);

        result.ShouldBeSuccess(removed => removed.Should().BeFalse());
    }

    [Fact]
    public async Task ResetAsync_ShouldOnlyTouchTheGivenCallsign()
    {
        WriteRequest();
        var otherRequest = Path.Combine(_pkiDirectory, "HB9AUTRE-H.csr");
        File.WriteAllText(otherRequest, "-----BEGIN CERTIFICATE REQUEST-----");

        await CreateService().ResetAsync(Callsign);

        File.Exists(otherRequest).Should().BeTrue();
    }

    [Fact]
    public async Task ResetAsync_WithoutACallsign_ShouldFail()
    {
        var result = await CreateService().ResetAsync("  ");

        result.ShouldBeFail();
    }

    #endregion

    public void Dispose()
    {
        try { Directory.Delete(_pkiDirectory, recursive: true); } catch { /* rien à nettoyer */ }
    }
}
