using FluentAssertions;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Infrastructure.Network;

namespace SvxlinkManagerV2.Infrastructure.Tests.Network;

/// <summary>
/// Tests unitaires pour les helpers purs du workflow de mise à jour applicative.
/// </summary>
public class ApplicationUpdateWorkflowServiceTests
{
    [Fact]
    public void ResolveStagingDirectory_WithRelativePath_ShouldResolveFromAppBaseDirectory()
    {
        var resolved = ApplicationUpdateWorkflowService.ResolveStagingDirectory("data/updates");

        resolved.Should().EndWith(Path.Combine("data", "updates"));
        Path.IsPathRooted(resolved).Should().BeTrue();
    }

    [Fact]
    public void ExpandInstallArguments_ShouldReplaceAllKnownTokens()
    {
        var package = new ApplicationDownloadedPackageInfo(
            Version: "0.1.0-alpha.195",
            FileName: "svxlinkmanagerv2_0.1.0-alpha.195_armhf.deb",
            FilePath: "/opt/svxlinkmanagerv2/data/updates/svxlinkmanagerv2_0.1.0-alpha.195_armhf.deb",
            FileSizeBytes: 1024,
            DownloadedAt: DateTimeOffset.UtcNow,
            SourceUrl: "https://example.invalid/package.deb");

        var arguments = ApplicationUpdateWorkflowService.ExpandInstallArguments(
            "--package {packagePath} --dir {packageDirectory} --name {packageName} --version {version}",
            package);

        arguments.Should().Contain("--package /opt/svxlinkmanagerv2/data/updates/svxlinkmanagerv2_0.1.0-alpha.195_armhf.deb");
        arguments.Should().Contain("--dir /opt/svxlinkmanagerv2/data/updates");
        arguments.Should().Contain("--name svxlinkmanagerv2_0.1.0-alpha.195_armhf.deb");
        arguments.Should().Contain("--version 0.1.0-alpha.195");
    }

    [Fact]
    public void ExtractSha256FromChecksumContent_ShouldReturnMatchingHash()
    {
        var hash = new string('a', 64);
        var content = $"{hash}  svxlinkmanagerv2_0.1.0-alpha.195_armhf.deb";

        var extracted = ApplicationUpdateWorkflowService.ExtractSha256FromChecksumContent(
            content,
            "svxlinkmanagerv2_0.1.0-alpha.195_armhf.deb");

        extracted.Should().Be(hash);
    }
}