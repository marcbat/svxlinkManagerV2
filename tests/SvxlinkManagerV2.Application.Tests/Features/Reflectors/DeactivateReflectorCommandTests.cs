using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Reflectors.DeactivateReflector;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Reflectors;

/// <summary>
/// Tests unitaires pour DeactivateReflectorCommand et son handler.
/// Le handler vérifie que le reflector est actif, arrête le daemon puis met à jour le tracker.
/// </summary>
public class DeactivateReflectorCommandTests
{
    private readonly IActiveSessionTracker _tracker;
    private readonly IReflectorDaemonService _daemonService;
    private readonly ILogger<DeactivateReflectorCommandHandler> _logger;

    public DeactivateReflectorCommandTests()
    {
        _tracker = Substitute.For<IActiveSessionTracker>();
        _daemonService = Substitute.For<IReflectorDaemonService>();
        _logger = Substitute.For<ILogger<DeactivateReflectorCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WhenReflectorIsActive_ShouldDeactivateAndUpdateTracker()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var command = new DeactivateReflectorCommand(reflectorId);

        _tracker.IsReflectorActive(reflectorId).Returns(true);
        _daemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeSuccess();
        _tracker.Received(1).SetActiveReflector(null);
    }

    [Fact]
    public async Task Handle_WhenReflectorIsNotActive_ShouldFail()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var command = new DeactivateReflectorCommand(reflectorId);

        _tracker.IsReflectorActive(reflectorId).Returns(false);

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_NOT_ACTIVE");
        });
        await _daemonService.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
        _tracker.DidNotReceive().SetActiveReflector(Arg.Any<Guid?>());
    }

    [Fact]
    public async Task Handle_WhenDaemonStopFails_ShouldFail()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var command = new DeactivateReflectorCommand(reflectorId);

        _tracker.IsReflectorActive(reflectorId).Returns(true);
        _daemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(
                global::LanguageExt.Common.Error.New("STOP_ERROR")));

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_STOP_ERROR");
        });
        _tracker.DidNotReceive().SetActiveReflector(Arg.Any<Guid?>());
    }

    private Task<Validation<Error, Unit>> CallHandle(DeactivateReflectorCommand command)
    {
        var handler = new DeactivateReflectorCommandHandler(_tracker, _daemonService, _logger);
        return handler.Handle(command, CancellationToken.None);
    }
}
