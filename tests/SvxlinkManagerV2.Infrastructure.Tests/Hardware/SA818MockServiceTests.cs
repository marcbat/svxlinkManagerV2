using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Infrastructure.Hardware;

namespace SvxlinkManagerV2.Infrastructure.Tests.Hardware;

public class SA818MockServiceTests
{
    private readonly ILogger<SA818MockService> _logger;
    private readonly SA818MockService _sut;

    public SA818MockServiceTests()
    {
        _logger = Substitute.For<ILogger<SA818MockService>>();
        _sut = new SA818MockService(_logger);
    }

    [Fact]
    public async Task ConfigureAsync_ShouldReturnSuccess_WithValidCommands()
    {
        // Arrange
        var commands = new SA818CommandSet(
            DmoSetGroup: "AT+DMOSETGROUP=0,145.5000,145.5000,0000,4,0000",
            DmoSetVolume: "AT+DMOSETVOLUME=4",
            SetFilter: "AT+SETFILTER=0,0,0"
        );

        // Act
        var result = await _sut.ConfigureAsync(commands);

        // Assert
        result.ShouldBeSuccess();
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("MOCK: Configuration du module SA818")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task ConfigureAsync_ShouldLogAllCommands()
    {
        // Arrange
        var commands = new SA818CommandSet(
            DmoSetGroup: "AT+DMOSETGROUP=0,145.5000,145.5000,0000,4,0000",
            DmoSetVolume: "AT+DMOSETVOLUME=4",
            SetFilter: "AT+SETFILTER=0,0,0"
        );

        // Act
        await _sut.ConfigureAsync(commands);

        // Assert
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(commands.DmoSetGroup)),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(commands.DmoSetVolume)),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(commands.SetFilter)),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task ConfigureAsync_ShouldHandleCancellation()
    {
        // Arrange
        var commands = new SA818CommandSet(
            DmoSetGroup: "AT+DMOSETGROUP=0,145.5000,145.5000,0000,4,0000",
            DmoSetVolume: "AT+DMOSETVOLUME=4",
            SetFilter: "AT+SETFILTER=0,0,0"
        );
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _sut.ConfigureAsync(commands, cts.Token);

        // Assert
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task IsConnectedAsync_ShouldReturnTrue()
    {
        // Act
        var result = await _sut.IsConnectedAsync();

        // Assert
        result.ShouldBeSuccess(value => value.Should().BeTrue());
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("MOCK: Module SA818 connecté")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task IsConnectedAsync_ShouldHandleCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _sut.IsConnectedAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<TaskCanceledException>();
    }
}
