using FluentAssertions;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Infrastructure.Network;

namespace SvxlinkManagerV2.Infrastructure.Tests.Network;

/// <summary>
/// Tests unitaires pour la sélection et la comparaison de releases GitHub.
/// </summary>
public class GitHubReleaseUpdateServiceTests
{
    [Fact]
    public void NormalizeVersion_ShouldConvertDebianPreReleaseToSemVer()
    {
        GitHubReleaseUpdateService.NormalizeVersion("1.4.0~alpha.7+abcdef")
            .Should().Be("1.4.0-alpha.7");
    }

    [Fact]
    public void Compare_ShouldTreatStableAsNewerThanPrerelease()
    {
        GitHubReleaseUpdateService.TryParseComparableVersion("1.2.0", out var stable).Should().BeTrue();
        GitHubReleaseUpdateService.TryParseComparableVersion("1.2.0-rc.2", out var releaseCandidate).Should().BeTrue();

        GitHubReleaseUpdateService.Compare(stable, releaseCandidate).Should().BeGreaterThan(0);
    }

    [Fact]
    public void MatchesChannel_ShouldRecognizeFeatureRelease()
    {
        var release = new GitHubReleaseUpdateService.GitHubRelease
        {
            TagName = "v1.3.0-monfeature.4",
            Prerelease = true,
            Assets =
            [
                new GitHubReleaseUpdateService.GitHubReleaseAsset
                {
                    Name = "svxlinkmanagerv2_1.3.0-monfeature.4_armhf.deb",
                    BrowserDownloadUrl = "https://example.invalid/file.deb"
                }
            ]
        };

        GitHubReleaseUpdateService.MatchesChannel(release, ApplicationUpdateChannel.Feature).Should().BeTrue();
        GitHubReleaseUpdateService.MatchesChannel(release, ApplicationUpdateChannel.Prerelease).Should().BeFalse();
    }

    [Fact]
    public void SelectLatestRelease_ShouldPickNewestStableWithPackage()
    {
        var releases = new[]
        {
            new GitHubReleaseUpdateService.GitHubRelease
            {
                TagName = "v1.1.0",
                Prerelease = false,
                PublishedAt = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
                Assets =
                [
                    new GitHubReleaseUpdateService.GitHubReleaseAsset
                    {
                        Name = "svxlinkmanagerv2_1.1.0_armhf.deb",
                        BrowserDownloadUrl = "https://example.invalid/1.1.0.deb"
                    }
                ]
            },
            new GitHubReleaseUpdateService.GitHubRelease
            {
                TagName = "v1.2.0-rc.1",
                Prerelease = true,
                PublishedAt = new DateTimeOffset(2026, 4, 2, 10, 0, 0, TimeSpan.Zero),
                Assets =
                [
                    new GitHubReleaseUpdateService.GitHubReleaseAsset
                    {
                        Name = "svxlinkmanagerv2_1.2.0-rc.1_armhf.deb",
                        BrowserDownloadUrl = "https://example.invalid/1.2.0-rc.1.deb"
                    }
                ]
            },
            new GitHubReleaseUpdateService.GitHubRelease
            {
                TagName = "v1.0.9",
                Prerelease = false,
                PublishedAt = new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero),
                Assets =
                [
                    new GitHubReleaseUpdateService.GitHubReleaseAsset
                    {
                        Name = "svxlinkmanagerv2_1.0.9_armhf.deb",
                        BrowserDownloadUrl = "https://example.invalid/1.0.9.deb"
                    }
                ]
            }
        };

        var latest = GitHubReleaseUpdateService.SelectLatestRelease(releases, ApplicationUpdateChannel.Stable, ".deb");

        latest.Should().NotBeNull();
        latest!.TagName.Should().Be("v1.1.0");
    }
}