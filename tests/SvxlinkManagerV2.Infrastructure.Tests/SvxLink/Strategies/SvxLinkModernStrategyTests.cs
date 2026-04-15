using FluentAssertions;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Infrastructure.SvxLink.Strategies;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink.Strategies;

public class SvxLinkModernStrategyTests
{
    private readonly SvxLinkModernStrategy _strategy = new();

    [Fact]
    public void Protocol_ShouldBeV3()
    {
        _strategy.Protocol.Should().Be(ReflectorProtocol.V3);
    }

    [Fact]
    public void BinaryPath_ShouldPointToModernPrefix()
    {
        _strategy.BinaryPath.Should().Be("/opt/svxlink-modern/bin/svxlink");
    }

    [Fact]
    public void LibraryPath_ShouldPointToModernPrefix()
    {
        _strategy.LibraryPath.Should().Be("/opt/svxlink-modern/lib");
    }

    [Fact]
    public void ConfigDirectory_ShouldPointToModernPrefix()
    {
        _strategy.ConfigDirectory.Should().Be("/opt/svxlink-modern/etc/svxlink");
    }

    [Fact]
    public void SoundsDirectory_ShouldPointToModernPrefix()
    {
        _strategy.SoundsDirectory.Should().Be("/opt/svxlink-modern/share/svxlink/sounds/fr_FR/svxlinkmanager");
    }

    [Fact]
    public void EventsDirectory_ShouldPointToModernPrefix()
    {
        _strategy.EventsDirectory.Should().Be("/opt/svxlink-modern/share/svxlink/events.d/local");
    }

    [Fact]
    public void EnvironmentVariables_ShouldContainLdLibraryPath()
    {
        _strategy.EnvironmentVariables.Should().ContainKey("LD_LIBRARY_PATH");
        _strategy.EnvironmentVariables["LD_LIBRARY_PATH"].Should().Be("/opt/svxlink-modern/lib");
    }
}
