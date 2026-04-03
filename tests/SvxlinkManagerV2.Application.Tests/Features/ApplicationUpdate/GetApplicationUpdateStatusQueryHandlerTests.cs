using FluentAssertions;
using LanguageExt;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate.GetApplicationUpdateStatus;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Tests.Features.ApplicationUpdate;

/// <summary>
/// Tests unitaires pour GetApplicationUpdateStatusQueryHandler.
/// </summary>
public class GetApplicationUpdateStatusQueryHandlerTests
{
    private readonly IApplicationUpdateService _applicationUpdateService;
    private readonly GetApplicationUpdateStatusQueryHandler _handler;

    public GetApplicationUpdateStatusQueryHandlerTests()
    {
        _applicationUpdateService = Substitute.For<IApplicationUpdateService>();
        _handler = new GetApplicationUpdateStatusQueryHandler(_applicationUpdateService);
    }

    [Fact]
    public async Task Handle_ShouldReturnServiceResult()
    {
        var expectedStatus = new ApplicationUpdateStatusDto(
            CurrentVersion: "1.0.0",
            Channel: ApplicationUpdateChannel.Stable,
            IsConfigured: true,
            IsUpdateAvailable: true,
            LatestRelease: new ApplicationReleaseInfo(
                Version: "1.1.0",
                Tag: "v1.1.0",
                Name: "SvxlinkManagerV2 1.1.0",
                PublishedAt: DateTimeOffset.UtcNow,
                IsPrerelease: false,
                ReleaseNotesUrl: "https://example.invalid/release",
                ChecksumUrl: "https://example.invalid/package.sha256",
                PackageUrl: "https://example.invalid/package.deb",
                PackageName: "svxlinkmanagerv2_1.1.0_armhf.deb"),
            Message: "Update disponible");

        _applicationUpdateService
            .GetStatusAsync(ApplicationUpdateChannel.Stable, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<Error, ApplicationUpdateStatusDto>.Success(expectedStatus)));

        var result = await _handler.Handle(
            new GetApplicationUpdateStatusQuery(ApplicationUpdateChannel.Stable),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: status => status.Should().BeEquivalentTo(expectedStatus),
            Fail: _ => Assert.Fail("Expected success"));
    }

    [Fact]
    public async Task Handle_WhenServiceFails_ShouldReturnFailure()
    {
        _applicationUpdateService
            .GetStatusAsync(ApplicationUpdateChannel.Feature, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Error.Validation("APPLICATION_UPDATE_ERROR", "GitHub indisponible").ToFailure<ApplicationUpdateStatusDto>()));

        var result = await _handler.Handle(
            new GetApplicationUpdateStatusQuery(ApplicationUpdateChannel.Feature),
            CancellationToken.None);

        result.IsFail.Should().BeTrue();
    }
}