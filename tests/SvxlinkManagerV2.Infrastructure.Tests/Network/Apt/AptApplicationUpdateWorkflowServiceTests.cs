using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Infrastructure.Network.Apt;

namespace SvxlinkManagerV2.Infrastructure.Tests.Network.Apt;

public class AptApplicationUpdateWorkflowServiceTests
{
    private readonly IApplicationUpdateService _updateService = Substitute.For<IApplicationUpdateService>();
    private readonly IAptCommandRunner _runner = Substitute.For<IAptCommandRunner>();
    private readonly AptUpdateOptions _options = new() { PackageName = "svxlinkmanagerv2" };

    private AptApplicationUpdateWorkflowService CreateService() => new(
        _updateService,
        _runner,
        NullLogger<AptApplicationUpdateWorkflowService>.Instance,
        Options.Create(_options));

    private void GivenUpdateAvailable(string version = "1.6.0")
    {
        var release = new ApplicationReleaseInfo(
            Version: version,
            Tag: $"v{version}",
            Name: $"svxlinkmanagerv2 {version}",
            PublishedAt: DateTimeOffset.UtcNow,
            IsPrerelease: false,
            ReleaseNotesUrl: null,
            ChecksumUrl: null,
            PackageUrl: null,
            PackageName: null);

        _updateService.GetStatusAsync(Arg.Any<ApplicationUpdateChannel?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, ApplicationUpdateStatusDto>.Success(
                new ApplicationUpdateStatusDto("1.5.1", ApplicationUpdateChannel.Stable, true, true, release, null)));
    }

    private void GivenNoUpdate()
    {
        _updateService.GetStatusAsync(Arg.Any<ApplicationUpdateChannel?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, ApplicationUpdateStatusDto>.Success(
                new ApplicationUpdateStatusDto("1.5.1", ApplicationUpdateChannel.Stable, true, false, null, null)));
    }

    private void GivenCommand(string fileName, int exitCode, string stderr = "")
    {
        _runner.RunAsync(fileName, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new AptCommandResult(exitCode, string.Empty, stderr));
    }

    [Fact]
    public async Task DownloadLatest_telecharge_sans_installer()
    {
        GivenUpdateAvailable();
        GivenCommand("apt-get", 0);

        var result = await CreateService().DownloadLatestAsync();

        result.ShouldBeSuccess(dto =>
        {
            dto.OperationState.Should().Be(ApplicationUpdateOperationState.Downloaded);
            dto.CanRequestInstall.Should().BeTrue();
        });

        // Le téléchargement ne doit jamais déclencher l'installation : c'est tout l'intérêt
        // du workflow en deux temps, qui laisse l'opérateur choisir le moment de la coupure.
        await _runner.Received(1).RunAsync(
            "apt-get",
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("--download-only")),
            Arg.Any<CancellationToken>());
        await _runner.DidNotReceive().RunAsync(
            "systemd-run", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadLatest_epingle_la_version_proposee()
    {
        GivenUpdateAvailable("1.6.0~beta.2");
        GivenCommand("apt-get", 0);

        await CreateService().DownloadLatestAsync();

        await _runner.Received(1).RunAsync(
            "apt-get",
            Arg.Is<IReadOnlyList<string>>(a => a.Contains("svxlinkmanagerv2=1.6.0~beta.2")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadLatest_remonte_l_erreur_apt()
    {
        GivenUpdateAvailable();
        GivenCommand("apt-get", 100, "E: Unable to fetch some archives");

        var result = await CreateService().DownloadLatestAsync();

        result.ShouldBeFail(errors => errors.Head.Message.Should().Contain("Unable to fetch"));
    }

    [Fact]
    public async Task DownloadLatest_ne_fait_rien_sans_mise_a_jour_disponible()
    {
        GivenNoUpdate();

        var result = await CreateService().DownloadLatestAsync();

        result.ShouldBeSuccess(dto => dto.OperationState.Should().Be(ApplicationUpdateOperationState.Idle));
        await _runner.DidNotReceive().RunAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestInstall_refuse_tant_que_rien_n_est_telecharge()
    {
        GivenUpdateAvailable();

        var result = await CreateService().RequestInstallAsync();

        result.ShouldBeFail(errors => errors.Head.Message.Should().Contain("Aucun paquet"));
    }

    [Fact]
    public async Task RequestInstall_delegue_a_systemd_run()
    {
        GivenUpdateAvailable();
        GivenCommand("apt-get", 0);
        GivenCommand("systemd-run", 0);

        var service = CreateService();
        await service.DownloadLatestAsync();
        var result = await service.RequestInstallAsync();

        result.ShouldBeSuccess(dto =>
            dto.OperationState.Should().Be(ApplicationUpdateOperationState.InstallRequested));

        // L'installation doit être détachée du service : le postinst le redémarre, ce qui
        // tuerait apt en pleine transaction dpkg s'il tournait dans ce processus.
        await _runner.Received(1).RunAsync(
            "systemd-run",
            Arg.Is<IReadOnlyList<string>>(a => a.Any(x => x.Contains("apt-get install"))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestInstall_remonte_l_echec_de_systemd_run()
    {
        GivenUpdateAvailable();
        GivenCommand("apt-get", 0);
        GivenCommand("systemd-run", 1, "Failed to start transient service");

        var service = CreateService();
        await service.DownloadLatestAsync();
        var result = await service.RequestInstallAsync();

        result.ShouldBeFail(errors => errors.Head.Message.Should().Contain("transient service"));
    }

    [Fact]
    public async Task L_installation_n_est_pas_proposee_si_la_version_disponible_a_change()
    {
        GivenUpdateAvailable("1.6.0");
        GivenCommand("apt-get", 0);

        var service = CreateService();
        await service.DownloadLatestAsync();

        // L'opérateur change de canal entre les deux étapes : le paquet en cache ne
        // correspond plus, l'installation ne doit pas rester proposée.
        GivenUpdateAvailable("1.7.0~alpha.1");
        var status = await service.GetStatusAsync();

        status.ShouldBeSuccess(dto => dto.CanRequestInstall.Should().BeFalse());
    }
}
