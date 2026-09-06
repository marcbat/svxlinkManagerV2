using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Infrastructure.Network.Apt;

namespace SvxlinkManagerV2.Infrastructure.Tests.Network.Apt;

public class AptApplicationUpdateServiceTests
{
    private readonly IAptCommandRunner _runner = Substitute.For<IAptCommandRunner>();
    private readonly IAptSourceManager _sourceManager = Substitute.For<IAptSourceManager>();
    private readonly AptUpdateOptions _options = new() { PackageName = "svxlinkmanagerv2" };

    private AptApplicationUpdateService CreateService() => new(
        _runner,
        _sourceManager,
        NullLogger<AptApplicationUpdateService>.Instance,
        Options.Create(_options));

    private void GivenPolicy(string installed, string candidate)
    {
        _runner.RunAsync("apt-get", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new AptCommandResult(0, string.Empty, string.Empty));

        _runner.RunAsync("apt-cache", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new AptCommandResult(0,
                $"svxlinkmanagerv2:\n  Installed: {installed}\n  Candidate: {candidate}\n  Version table:\n",
                string.Empty));
    }

    private void GivenComparison(bool candidateIsNewer)
    {
        _runner.RunAsync("dpkg", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new AptCommandResult(candidateIsNewer ? 0 : 1, string.Empty, string.Empty));
    }

    [Fact]
    public async Task GetStatus_signale_une_mise_a_jour_quand_le_candidat_est_plus_recent()
    {
        _sourceManager.ReadChannel().Returns(ApplicationUpdateChannel.Stable);
        GivenPolicy("1.4.0", "1.5.1");
        GivenComparison(candidateIsNewer: true);

        var result = await CreateService().GetStatusAsync(ApplicationUpdateChannel.Stable);

        result.ShouldBeSuccess(status =>
        {
            status.IsUpdateAvailable.Should().BeTrue();
            status.CurrentVersion.Should().Be("1.4.0");
            status.LatestRelease!.Version.Should().Be("1.5.1");
            status.LatestRelease.Tag.Should().Be("v1.5.1");
            status.LatestRelease.ReleaseNotesUrl.Should().Contain("/releases/tag/v1.5.1");
        });
    }

    [Fact]
    public async Task GetStatus_ne_signale_rien_quand_la_version_installee_est_la_candidate()
    {
        _sourceManager.ReadChannel().Returns(ApplicationUpdateChannel.Stable);
        GivenPolicy("1.5.1", "1.5.1");

        var result = await CreateService().GetStatusAsync(ApplicationUpdateChannel.Stable);

        result.ShouldBeSuccess(status =>
        {
            status.IsUpdateAvailable.Should().BeFalse();
            status.LatestRelease.Should().BeNull();
        });
    }

    [Fact]
    public async Task GetStatus_marque_une_preversion_comme_telle()
    {
        _sourceManager.ReadChannel().Returns(ApplicationUpdateChannel.Development);
        GivenPolicy("1.5.1", "1.6.0~alpha.3");
        GivenComparison(candidateIsNewer: true);

        var result = await CreateService().GetStatusAsync(ApplicationUpdateChannel.Development);

        result.ShouldBeSuccess(status =>
        {
            status.LatestRelease!.IsPrerelease.Should().BeTrue();
            // Le tilde Debian correspond au tiret de la version sémantique du tag.
            status.LatestRelease.Tag.Should().Be("v1.6.0-alpha.3");
        });
    }

    [Fact]
    public async Task GetStatus_ecrit_la_source_quand_le_canal_demande_differe_du_canal_actif()
    {
        _sourceManager.ReadChannel().Returns(ApplicationUpdateChannel.Stable);
        _sourceManager.WriteChannel(Arg.Any<ApplicationUpdateChannel>())
            .Returns(Validation<Error, Unit>.Success(Unit.Default));
        GivenPolicy("1.5.1", "1.5.1");

        await CreateService().GetStatusAsync(ApplicationUpdateChannel.Beta);

        _sourceManager.Received(1).WriteChannel(ApplicationUpdateChannel.Beta);
    }

    [Fact]
    public async Task GetStatus_n_ecrit_pas_la_source_quand_le_canal_est_deja_le_bon()
    {
        _sourceManager.ReadChannel().Returns(ApplicationUpdateChannel.Beta);
        GivenPolicy("1.5.1", "1.5.1");

        await CreateService().GetStatusAsync(ApplicationUpdateChannel.Beta);

        _sourceManager.DidNotReceive().WriteChannel(Arg.Any<ApplicationUpdateChannel>());
    }

    [Fact]
    public async Task GetStatus_remonte_l_erreur_reelle_quand_apt_get_update_echoue()
    {
        _sourceManager.ReadChannel().Returns(ApplicationUpdateChannel.Stable);
        _runner.RunAsync("apt-get", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new AptCommandResult(100, string.Empty, "E: Could not resolve 'marcbat.github.io'"));

        var result = await CreateService().GetStatusAsync(ApplicationUpdateChannel.Stable);

        result.ShouldBeFail(errors =>
            errors.Head.Message.Should().Contain("Could not resolve"));
    }

    [Fact]
    public async Task GetStatus_remonte_l_erreur_reelle_quand_apt_cache_echoue()
    {
        _sourceManager.ReadChannel().Returns(ApplicationUpdateChannel.Stable);
        _runner.RunAsync("apt-get", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new AptCommandResult(0, string.Empty, string.Empty));
        _runner.RunAsync("apt-cache", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new AptCommandResult(1, string.Empty, "apt-cache: introuvable"));

        var result = await CreateService().GetStatusAsync(ApplicationUpdateChannel.Stable);

        result.ShouldBeFail(errors => errors.Head.Message.Should().Contain("introuvable"));
    }

    [Fact]
    public async Task GetStatus_indique_l_absence_de_paquet_sur_le_canal()
    {
        _sourceManager.ReadChannel().Returns(ApplicationUpdateChannel.Stable);
        GivenPolicy("1.5.1", "(none)");

        var result = await CreateService().GetStatusAsync(ApplicationUpdateChannel.Stable);

        result.ShouldBeSuccess(status =>
        {
            status.IsUpdateAvailable.Should().BeFalse();
            status.Message.Should().Contain("Aucune version");
        });
    }

    [Fact]
    public async Task GetStatus_desactive_ne_lance_aucune_commande()
    {
        _options.Enabled = false;

        var result = await CreateService().GetStatusAsync();

        result.ShouldBeSuccess(status => status.IsConfigured.Should().BeFalse());
        await _runner.DidNotReceive().RunAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("svxlinkmanagerv2:\n  Installed: 1.4.0\n  Candidate: 1.5.1\n", "1.4.0", "1.5.1")]
    [InlineData("svxlinkmanagerv2:\n  Installed: (none)\n  Candidate: 1.5.1\n", null, "1.5.1")]
    [InlineData("svxlinkmanagerv2:\n  Installed: 1.5.1\n  Candidate: (none)\n", "1.5.1", null)]
    public void ParsePolicy_extrait_les_versions(string output, string? installed, string? candidate)
    {
        var parsed = AptApplicationUpdateService.ParsePolicy(output);

        parsed.Installed.Should().Be(installed);
        parsed.Candidate.Should().Be(candidate);
    }

    [Theory]
    [InlineData("1.5.1", "v1.5.1")]
    [InlineData("1.5.0~alpha.8", "v1.5.0-alpha.8")]
    [InlineData("1.5.0~beta.1", "v1.5.0-beta.1")]
    public void ToReleaseTag_convertit_la_version_debian_en_tag(string version, string expected)
    {
        AptApplicationUpdateService.ToReleaseTag(version).Should().Be(expected);
    }
}
