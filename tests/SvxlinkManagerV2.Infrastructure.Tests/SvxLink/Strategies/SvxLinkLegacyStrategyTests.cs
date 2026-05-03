using FluentAssertions;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Infrastructure.SvxLink.Strategies;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink.Strategies;

public class SvxLinkLegacyStrategyTests
{
    private readonly SvxLinkLegacyStrategy _strategy = new();

    [Fact]
    public void Protocol_ShouldBeV2()
    {
        _strategy.Protocol.Should().Be(ReflectorProtocol.V2);
    }

    [Fact]
    public void BinaryPath_ShouldPointToLegacyPrefix()
    {
        _strategy.BinaryPath.Should().Be("/opt/svxlink-legacy/bin/svxlink");
    }

    [Fact]
    public void LibraryPath_ShouldPointToLegacyPrefix()
    {
        _strategy.LibraryPath.Should().Be("/opt/svxlink-legacy/lib");
    }

    [Fact]
    public void ConfigDirectory_ShouldPointToLegacyPrefix()
    {
        _strategy.ConfigDirectory.Should().Be("/opt/svxlink-legacy/etc/svxlink");
    }

    [Fact]
    public void SoundsDirectory_ShouldPointToLegacyPrefix()
    {
        _strategy.SoundsDirectory.Should().Be("/opt/svxlink-legacy/share/svxlink/sounds/fr_FR/svxlinkmanager");
    }

    [Fact]
    public void EventsDirectory_ShouldPointToLegacyPrefix()
    {
        _strategy.EventsDirectory.Should().Be("/opt/svxlink-legacy/share/svxlink/events.d/local");
    }

    [Fact]
    public void EnvironmentVariables_ShouldContainLdLibraryPath()
    {
        _strategy.EnvironmentVariables.Should().ContainKey("LD_LIBRARY_PATH");
        _strategy.EnvironmentVariables["LD_LIBRARY_PATH"].Should().Be("/opt/svxlink-legacy/lib");
    }
}
