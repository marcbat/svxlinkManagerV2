using FluentAssertions;
using SvxlinkManagerV2.Application.Features.Ping;

namespace SvxlinkManagerV2.Application.Tests.Features.Ping;

/// <summary>
/// Tests unitaires pour PingCommand et son handler.
/// </summary>
public class PingCommandTests
{
    [Fact]
    public async Task Handle_ShouldReturnPongWithMessage()
    {
        // Arrange
        var command = new PingCommand("test");
        var handler = new PingCommandHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be("Pong: test");
    }

    [Fact]
    public async Task Handle_WithEmptyMessage_ShouldReturnPongWithEmptyString()
    {
        // Arrange
        var command = new PingCommand(string.Empty);
        var handler = new PingCommandHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be("Pong: ");
    }
}
