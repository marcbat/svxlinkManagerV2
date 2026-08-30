using FluentAssertions;
using SvxlinkManagerV2.Infrastructure.Network;

namespace SvxlinkManagerV2.Infrastructure.Tests.Network;

/// <summary>
/// Tests unitaires pour WifiLinkReader (analyse des sorties nmcli terse
/// et de /proc/net/wireless).
/// </summary>
public class WifiLinkReaderTests
{
    // -------------------------------------------------------------------------
    // Découpage terse
    // -------------------------------------------------------------------------

    [Fact]
    public void SplitTerseLine_ShouldSplitOnColons()
    {
        var fields = WifiLinkReader.SplitTerseLine("wlan0:wifi:connected:HomeNetwork");

        fields.Should().Equal("wlan0", "wifi", "connected", "HomeNetwork");
    }

    [Fact]
    public void SplitTerseLine_ShouldPreserveEscapedColons()
    {
        var fields = WifiLinkReader.SplitTerseLine(@"wlan0:wifi:connected:Mon\:Reseau");

        fields.Should().Equal("wlan0", "wifi", "connected", "Mon:Reseau");
    }

    // -------------------------------------------------------------------------
    // Périphérique actif
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseActiveDevice_ShouldPreferConnectedWifiDevice()
    {
        var output = string.Join('\n',
            "eth0:ethernet:connected:Filaire",
            "wlan0:wifi:connected:HomeNetwork",
            "lo:loopback:unmanaged:");

        var active = WifiLinkReader.ParseActiveDevice(output);

        active.Should().NotBeNull();
        active!.Value.Device.Should().Be("wlan0");
        active.Value.Connection.Should().Be("HomeNetwork");
    }

    [Fact]
    public void ParseActiveDevice_WithoutWifi_ShouldFallBackOnOtherConnectedDevice()
    {
        var output = string.Join('\n',
            "eth0:ethernet:connected:Filaire",
            "lo:loopback:unmanaged:");

        var active = WifiLinkReader.ParseActiveDevice(output);

        active.Should().NotBeNull();
        active!.Value.Device.Should().Be("eth0");
    }

    [Fact]
    public void ParseActiveDevice_ShouldIgnoreLoopbackAndDisconnectedDevices()
    {
        var output = string.Join('\n',
            "wlan0:wifi:disconnected:",
            "lo:loopback:connected (externally):");

        WifiLinkReader.ParseActiveDevice(output).Should().BeNull();
    }

    [Fact]
    public void ParseActiveDevice_WithEmptyOutput_ShouldReturnNull()
    {
        WifiLinkReader.ParseActiveDevice(string.Empty).Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Adresse IP
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseIpAddress_ShouldReturnAddressWithoutPrefix()
    {
        var output = "IP4.ADDRESS[1]:192.168.1.42/24\nIP4.GATEWAY:192.168.1.1";

        WifiLinkReader.ParseIpAddress(output).Should().Be("192.168.1.42");
    }

    [Fact]
    public void ParseIpAddress_WithoutAddressLine_ShouldReturnNull()
    {
        WifiLinkReader.ParseIpAddress("IP4.GATEWAY:192.168.1.1").Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Qualité du lien
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseSignalPercent_ShouldConvertLinkQualityToPercent()
    {
        var content = string.Join('\n',
            "Inter-| sta-|   Quality        |   Discarded packets               | Missed | WE",
            " face | tus | link level noise |  nwid  crypt   frag  retry   misc | beacon | 22",
            " wlan0: 0000   35.  -60.  -256        0      0      0      0     0        0");

        WifiLinkReader.ParseSignalPercent(content, "wlan0").Should().Be(50);
    }

    [Fact]
    public void ParseSignalPercent_ShouldClampAboveHundred()
    {
        var content = " wlan0: 0000   90.  -40.  -256        0      0      0      0     0        0";

        WifiLinkReader.ParseSignalPercent(content, "wlan0").Should().Be(100);
    }

    [Fact]
    public void ParseSignalPercent_ForUnknownDevice_ShouldReturnNull()
    {
        var content = " wlan0: 0000   70.  -40.  -256        0      0      0      0     0        0";

        WifiLinkReader.ParseSignalPercent(content, "wlan1").Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Repli sur les interfaces du runtime
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadFromRuntimeInterfaces_ShouldNotThrow()
    {
        // Le repli doit rester exploitable quelle que soit la plateforme de test.
        var link = WifiLinkReader.ReadFromRuntimeInterfaces();

        link.Should().NotBeNull();
        link.Ssid.Should().BeNull();
    }
}
