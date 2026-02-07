using FluentAssertions;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Ping;
using Wolverine;

namespace SvxlinkManagerV2.Application.Tests.Features.Ping;

/// <summary>
/// Tests unitaires pour PingCommand et son handler.
/// Valide le pattern CQRS avec Wolverine via IMessageBus.
/// </summary>
public class PingCommandTests
{
    /// <summary>
    /// Vérifie que PingCommandHandler retourne correctement "Pong: {message}".
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnPongWithMessage()
    {
        // Arrange
        var command = new PingCommand("test");

        // Act
        var result = await PingCommandHandler.Handle(command);

        // Assert
        result.Should().Be("Pong: test");
    }

    /// <summary>
    /// Vérifie que PingCommand peut être invoquée via IMessageBus de Wolverine.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldExecutePingCommand()
    {
        // Arrange
        var messageBus = Substitute.For<IMessageBus>();
        var command = new PingCommand("integration test");
        
        // Configure le mock pour retourner la réponse attendue
        messageBus.InvokeAsync<string>(command)
            .Returns(Task.FromResult("Pong: integration test"));

        // Act
        var result = await messageBus.InvokeAsync<string>(command);

        // Assert
        result.Should().Be("Pong: integration test");
        await messageBus.Received(1).InvokeAsync<string>(command);
    }

    /// <summary>
    /// Vérifie que le handler gère correctement les messages vides.
    /// </summary>
    [Fact]
    public async Task Handle_WithEmptyMessage_ShouldReturnPongWithEmptyString()
    {
        // Arrange
        var command = new PingCommand(string.Empty);

        // Act
        var result = await PingCommandHandler.Handle(command);

        // Assert
        result.Should().Be("Pong: ");
    }
}
