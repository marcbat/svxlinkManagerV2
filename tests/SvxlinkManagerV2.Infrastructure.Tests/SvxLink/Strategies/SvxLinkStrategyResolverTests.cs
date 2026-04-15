using FluentAssertions;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Infrastructure.SvxLink.Strategies;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink.Strategies;

public class SvxLinkStrategyResolverTests
{
    [Fact]
    public void Resolve_WithV2_ShouldReturnLegacyStrategy()
    {
        // Arrange
        var resolver = CreateResolver();

        // Act
        var strategy = resolver.Resolve(ReflectorProtocol.V2);

        // Assert
        strategy.Should().BeOfType<SvxLinkLegacyStrategy>();
        strategy.Protocol.Should().Be(ReflectorProtocol.V2);
    }

    [Fact]
    public void Resolve_WithV3_ShouldReturnModernStrategy()
    {
        // Arrange
        var resolver = CreateResolver();

        // Act
        var strategy = resolver.Resolve(ReflectorProtocol.V3);

        // Assert
        strategy.Should().BeOfType<SvxLinkModernStrategy>();
        strategy.Protocol.Should().Be(ReflectorProtocol.V3);
    }

    [Fact]
    public void Resolve_WithUnknownProtocol_ShouldThrowArgumentException()
    {
        // Arrange
        var resolver = CreateResolver();

        // Act
        var act = () => resolver.Resolve((ReflectorProtocol)99);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetAll_ShouldReturnAllRegisteredStrategies()
    {
        // Arrange
        var resolver = CreateResolver();

        // Act
        var strategies = resolver.GetAll().ToList();

        // Assert
        strategies.Should().HaveCount(2);
        strategies.Should().Contain(s => s.Protocol == ReflectorProtocol.V2);
        strategies.Should().Contain(s => s.Protocol == ReflectorProtocol.V3);
    }

    private static SvxLinkStrategyResolver CreateResolver()
        => new(new ISvxLinkVersionStrategy[]
        {
            new SvxLinkLegacyStrategy(),
            new SvxLinkModernStrategy()
        });
}
