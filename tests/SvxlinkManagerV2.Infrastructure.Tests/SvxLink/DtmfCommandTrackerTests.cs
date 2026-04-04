using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests unitaires pour DtmfCommandTracker
/// </summary>
public class DtmfCommandTrackerTests
{
    private readonly ILogger<DtmfCommandTracker> _logger;
    private readonly ISvxLinkLogService _logService;

    public DtmfCommandTrackerTests()
    {
        _logger = Substitute.For<ILogger<DtmfCommandTracker>>();
        _logService = Substitute.For<ISvxLinkLogService>();
    }

    [Fact]
    public void Constructor_ShouldSubscribeToLogService()
    {
        // Arrange & Act
        var tracker = new DtmfCommandTracker(_logger, _logService);

        // Assert
        _logService.Received(1).OnLogReceived += Arg.Any<Action<SvxLinkLogEntry>>();
    }

    [Fact]
    public void OnLogReceived_WithDtmfCommand_ShouldFireEvent()
    {
        // Arrange
        var tracker = new DtmfCommandTracker(_logger, _logService);
        string? capturedCommand = null;
        tracker.OnDtmfCommandReceived += cmd => capturedCommand = cmd;

        var logEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "DTMF_CMD:96",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(logEntry);

        // Assert
        capturedCommand.Should().Be("96");
    }

    [Fact]
    public void OnLogReceived_WithDtmfCommandInLongerMessage_ShouldFireEvent()
    {
        // Arrange
        var tracker = new DtmfCommandTracker(_logger, _logService);
        string? capturedCommand = null;
        tracker.OnDtmfCommandReceived += cmd => capturedCommand = cmd;

        var logEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "SvxlinkManagerV2: DTMF_CMD:42",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(logEntry);

        // Assert
        capturedCommand.Should().Be("42");
    }

    [Fact]
    public void OnLogReceived_WithUnrelatedLog_ShouldNotFireEvent()
    {
        // Arrange
        var tracker = new DtmfCommandTracker(_logger, _logService);
        var eventCount = 0;
        tracker.OnDtmfCommandReceived += _ => eventCount++;

        var logEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Connected nodes: HB9GXP-H",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(logEntry);

        // Assert
        eventCount.Should().Be(0);
    }

    [Fact]
    public void OnLogReceived_WithEmptyDtmfCommand_ShouldNotFireEvent()
    {
        // Arrange
        var tracker = new DtmfCommandTracker(_logger, _logService);
        var eventCount = 0;
        tracker.OnDtmfCommandReceived += _ => eventCount++;

        var logEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "DTMF_CMD:",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(logEntry);

        // Assert
        eventCount.Should().Be(0);
    }

    [Fact]
    public void OnLogReceived_WithWhitespaceDtmfCommand_ShouldNotFireEvent()
    {
        // Arrange
        var tracker = new DtmfCommandTracker(_logger, _logService);
        var eventCount = 0;
        tracker.OnDtmfCommandReceived += _ => eventCount++;

        var logEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "DTMF_CMD:   ",
            SvxLinkLogLevel.Info
        );

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(logEntry);

        // Assert
        eventCount.Should().Be(0);
    }

    [Fact]
    public void OnLogReceived_WithMultipleDtmfCommands_ShouldFireMultipleEvents()
    {
        // Arrange
        var tracker = new DtmfCommandTracker(_logger, _logService);
        var commands = new List<string>();
        tracker.OnDtmfCommandReceived += cmd => commands.Add(cmd);

        var entry1 = new SvxLinkLogEntry(DateTime.Now, "DTMF_CMD:96", SvxLinkLogLevel.Info);
        var entry2 = new SvxLinkLogEntry(DateTime.Now, "DTMF_CMD:42", SvxLinkLogLevel.Info);

        // Act
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(entry1);
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(entry2);

        // Assert
        commands.Should().HaveCount(2);
        commands[0].Should().Be("96");
        commands[1].Should().Be("42");
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromLogService()
    {
        // Arrange
        var tracker = new DtmfCommandTracker(_logger, _logService);

        // Act
        tracker.Dispose();

        // Assert
        _logService.Received(1).OnLogReceived -= Arg.Any<Action<SvxLinkLogEntry>>();
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldOnlyUnsubscribeOnce()
    {
        // Arrange
        var tracker = new DtmfCommandTracker(_logger, _logService);

        // Act
        tracker.Dispose();
        tracker.Dispose();

        // Assert
        _logService.Received(1).OnLogReceived -= Arg.Any<Action<SvxLinkLogEntry>>();
    }
}
