using FluentAssertions;
using SvxlinkManagerV2.Application.Features.Ping;

namespace SvxlinkManagerV2.Application.Tests.Features.Ping;

/// <summary>
/// Tests unitaires pour GetPingQuery et son handler.
/// </summary>
public class GetPingQueryTests
{
    [Fact]
    public async Task Handle_ShouldReturnServiceAliveMessage()
    {
        // Arrange
        var query = new GetPingQuery();
        var handler = new GetPingQueryHandler();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be("Ping service is alive");
    }
}
