using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.DeactivateSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;
using LangExtError = LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour DeactivateSalonCommand et son handler.
/// </summary>
public class DeactivateSalonCommandTests
{
    private readonly IActiveSessionTracker _tracker;
    private readonly ISvxLinkDaemonService _daemonService;
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly ILogger<DeactivateSalonCommandHandler> _logger;

    public DeactivateSalonCommandTests()
    {
        _tracker = Substitute.For<IActiveSessionTracker>();
        _daemonService = Substitute.For<ISvxLinkDaemonService>();
        _connectedNodesService = Substitute.For<IConnectedNodesService>();
        _logger = Substitute.For<ILogger<DeactivateSalonCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WhenSalonIsActive_ShouldDeactivateSuccessfully()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var command = new DeactivateSalonCommand(salonId);

        _tracker.IsSalonActive(salonId).Returns(true);
        _daemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));

        // Act
        var result = await new DeactivateSalonCommandHandler(
            _tracker, _daemonService, _connectedNodesService, _logger)
            .Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();

        await _daemonService.Received(1).StopAsync(Arg.Any<CancellationToken>());
        _connectedNodesService.Received(1).Reset();
        _tracker.Received(1).SetActiveSalon(null);
    }

    [Fact]
    public async Task Handle_WhenSalonNotActive_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var command = new DeactivateSalonCommand(salonId);

        _tracker.IsSalonActive(salonId).Returns(false);

        // Act
        var result = await new DeactivateSalonCommandHandler(
            _tracker, _daemonService, _connectedNodesService, _logger)
            .Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_NOT_ACTIVE");
        });

        await _daemonService.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
        _connectedNodesService.DidNotReceive().Reset();
    }

    [Fact]
    public async Task Handle_WhenDaemonStopFails_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var command = new DeactivateSalonCommand(salonId);

        _tracker.IsSalonActive(salonId).Returns(true);
        _daemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(
                Validation<LangExtError, Unit>.Fail(Seq1<LangExtError>(LangExtError.New("Impossible d'arrêter le daemon")))));

        // Act
        var result = await new DeactivateSalonCommandHandler(
            _tracker, _daemonService, _connectedNodesService, _logger)
            .Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SVXLINK_STOP_ERROR");
        });

        _connectedNodesService.DidNotReceive().Reset();
        _tracker.DidNotReceive().SetActiveSalon(Arg.Any<Guid?>());
    }
}
