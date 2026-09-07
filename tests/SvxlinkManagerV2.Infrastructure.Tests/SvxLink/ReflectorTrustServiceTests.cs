using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests de la suppression du bundle CA mémorisé par le nœud.
/// Le service touche au vrai système de fichiers : les tests travaillent donc dans un
/// répertoire temporaire, comme ceux de génération de configuration SVXLink.
/// </summary>
public class ReflectorTrustServiceTests : IDisposable
{
    private readonly ILogger<ReflectorTrustService> _logger = Substitute.For<ILogger<ReflectorTrustService>>();
    private readonly string _pkiDirectory;

    public ReflectorTrustServiceTests()
    {
        _pkiDirectory = Path.Combine(Path.GetTempPath(), $"svxlink-pki-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_pkiDirectory);
    }

    private string CaBundlePath => Path.Combine(_pkiDirectory, "ca-bundle.crt");

    private ReflectorTrustService CreateService() => new(_logger, CaBundlePath);

    [Fact]
    public async Task ResetTrustAsync_ShouldDeleteTheCaBundle()
    {
        await File.WriteAllTextAsync(CaBundlePath, "-----BEGIN CERTIFICATE-----");

        var result = await CreateService().ResetTrustAsync();

        result.ShouldBeSuccess(removed => removed.Should().BeTrue());
        File.Exists(CaBundlePath).Should().BeFalse();
    }

    [Fact]
    public async Task ResetTrustAsync_WithoutBundle_ShouldSucceedWithoutRemoving()
    {
        // Idempotence : un nœud qui n'a encore rien téléchargé est déjà dans l'état voulu.
        var result = await CreateService().ResetTrustAsync();

        result.ShouldBeSuccess(removed => removed.Should().BeFalse());
    }

    [Fact]
    public async Task ResetTrustAsync_ShouldLeaveTheNodeIdentityUntouched()
    {
        // Effacer la clé privée ou le certificat signé imposerait de refaire signer une CSR
        // par l'administrateur du réflecteur : bien plus lourd que le problème à résoudre.
        var keyPath = Path.Combine(_pkiDirectory, "HB9GXP-H.key");
        var certPath = Path.Combine(_pkiDirectory, "HB9GXP-H.crt");
        await File.WriteAllTextAsync(keyPath, "clé privée");
        await File.WriteAllTextAsync(certPath, "certificat signé");
        await File.WriteAllTextAsync(CaBundlePath, "autorité");

        await CreateService().ResetTrustAsync();

        File.Exists(keyPath).Should().BeTrue();
        File.Exists(certPath).Should().BeTrue();
    }

    [Fact]
    public async Task ResetTrustAsync_WhenTheBundleCannotBeRemoved_ShouldFail()
    {
        // Un répertoire à la place du fichier : File.Delete lève, et l'échec doit remonter
        // plutôt que d'être avalé — sinon l'interface annoncerait une réparation imaginaire.
        Directory.CreateDirectory(CaBundlePath);

        var result = await CreateService().ResetTrustAsync();

        result.ShouldBeFail();
    }

    [Fact]
    public void CaBundlePath_ShouldDefaultToTheConfiguredPkiDirectory()
    {
        // CERT_PKI_DIR est écrit par SvxLinkConfigurationService : les deux doivent désigner
        // le même répertoire, sinon le service supprimerait un fichier qui n'existe pas.
        SvxLinkPkiPaths.CaBundleFile.Should().Be($"{SvxLinkPkiPaths.Directory}/ca-bundle.crt");
        SvxLinkPkiPaths.Directory.Should().Be("/var/lib/svxlink/pki");
    }

    public void Dispose()
    {
        try { Directory.Delete(_pkiDirectory, recursive: true); } catch { /* rien à nettoyer */ }
    }
}
