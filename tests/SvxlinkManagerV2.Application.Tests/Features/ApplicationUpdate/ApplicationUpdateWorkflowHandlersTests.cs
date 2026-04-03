using FluentAssertions;
using LanguageExt;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate.DownloadApplicationUpdate;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate.GetApplicationUpdateWorkflowStatus;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate.RequestApplicationUpdateInstallation;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Tests.Features.ApplicationUpdate;

/// <summary>
/// Tests unitaires pour les handlers du workflow de mise à jour applicative.
/// </summary>
public class ApplicationUpdateWorkflowHandlersTests
{
    private readonly IApplicationUpdateWorkflowService _workflowService;

    public ApplicationUpdateWorkflowHandlersTests()
    {
        _workflowService = Substitute.For<IApplicationUpdateWorkflowService>();
    }

    [Fact]
    public async Task GetStatusHandler_ShouldReturnWorkflowStatus()
    {
        var expected = CreateStatus();
        _workflowService.GetStatusAsync(ApplicationUpdateChannel.Prerelease, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<Error, ApplicationUpdateWorkflowStatusDto>.Success(expected)));

        var handler = new GetApplicationUpdateWorkflowStatusQueryHandler(_workflowService);
        var result = await handler.Handle(new GetApplicationUpdateWorkflowStatusQuery(ApplicationUpdateChannel.Prerelease), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: status => status.Should().BeEquivalentTo(expected),
            Fail: _ => Assert.Fail("Expected success"));
    }

    [Fact]
    public async Task DownloadHandler_ShouldDelegateToWorkflowService()
    {
        var expected = CreateStatus();
        _workflowService.DownloadLatestAsync(ApplicationUpdateChannel.Stable, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<Error, ApplicationUpdateWorkflowStatusDto>.Success(expected)));

        var handler = new DownloadApplicationUpdateCommandHandler(_workflowService);
        var result = await handler.Handle(new DownloadApplicationUpdateCommand(ApplicationUpdateChannel.Stable), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RequestInstallHandler_ShouldDelegateToWorkflowService()
    {
        var expected = CreateStatus() with { OperationState = ApplicationUpdateOperationState.InstallRequested };
        _workflowService.RequestInstallAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<Error, ApplicationUpdateWorkflowStatusDto>.Success(expected)));

        var handler = new RequestApplicationUpdateInstallationCommandHandler(_workflowService);
        var result = await handler.Handle(new RequestApplicationUpdateInstallationCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private static ApplicationUpdateWorkflowStatusDto CreateStatus()
        => new(
            UpdateStatus: new ApplicationUpdateStatusDto(
                CurrentVersion: "0.1.0-alpha.194",
                Channel: ApplicationUpdateChannel.Prerelease,
                IsConfigured: true,
                IsUpdateAvailable: true,
                LatestRelease: new ApplicationReleaseInfo(
                    Version: "0.1.0-alpha.195",
                    Tag: "v0.1.0-alpha.195",
                    Name: "SvxlinkManagerV2 0.1.0-alpha.195",
                    PublishedAt: DateTimeOffset.UtcNow,
                    IsPrerelease: true,
                    ReleaseNotesUrl: "https://example.invalid/release",
                    ChecksumUrl: "https://example.invalid/package.sha256",
                    PackageUrl: "https://example.invalid/package.deb",
                    PackageName: "svxlinkmanagerv2_0.1.0-alpha.195_armhf.deb"),
                Message: "Une nouvelle version est disponible."),
            OperationState: ApplicationUpdateOperationState.Idle,
            DownloadProgressPercent: null,
            DownloadedPackage: null,
            IsBusy: false,
            CanDownload: true,
            CanRequestInstall: false,
            LastOperationMessage: null,
            UpdatedAt: DateTimeOffset.UtcNow);
}