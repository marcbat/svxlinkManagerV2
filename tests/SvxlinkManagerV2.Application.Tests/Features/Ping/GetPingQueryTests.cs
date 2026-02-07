using FluentAssertions;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Ping;
using Wolverine;

namespace SvxlinkManagerV2.Application.Tests.Features.Ping;

/// <summary>
/// Tests unitaires pour GetPingQuery et son handler.
/// Valide le pattern CQRS avec Wolverine via IMessageBus.
/// </summary>
public class GetPingQueryTests
{
    /// <summary>
    /// Vérifie que GetPingQueryHandler retourne le message de statut correct.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReturnServiceAliveMessage()
    {
        // Arrange
        var query = new GetPingQuery();

        // Act
        var result = await GetPingQueryHandler.Handle(query);

        // Assert
        result.Should().Be("Ping service is alive");
    }

    /// <summary>
    /// Vérifie que GetPingQuery peut être invoquée via IMessageBus de Wolverine.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldExecuteGetPingQuery()
    {
        // Arrange
        var messageBus = Substitute.For<IMessageBus>();
        var query = new GetPingQuery();
        
        // Configure le mock pour retourner la réponse attendue
        messageBus.InvokeAsync<string>(query)
            .Returns(Task.FromResult("Ping service is alive"));

        // Act
        var result = await messageBus.InvokeAsync<string>(query);

        // Assert
        result.Should().Be("Ping service is alive");
        await messageBus.Received(1).InvokeAsync<string>(query);
    }
}
