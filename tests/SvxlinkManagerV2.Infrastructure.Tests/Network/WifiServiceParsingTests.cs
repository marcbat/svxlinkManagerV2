using FluentAssertions;
using SvxlinkManagerV2.Infrastructure.Network;

namespace SvxlinkManagerV2.Infrastructure.Tests.Network;

/// <summary>
/// Tests unitaires pour le parsing des sorties nmcli dans WifiService.
/// Utilise des données statiques représentant la sortie réelle de nmcli.
/// </summary>
public class WifiServiceParsingTests
{
    #region ParseNetworksOutput Tests

    [Fact]
    public void ParseNetworksOutput_WithValidOutput_ShouldReturnNetworks()
    {
        // Arrange - Sortie nmcli typique
        var output = """
            IN-USE  SSID            MODE   CHAN  RATE        SIGNAL  BARS  SECURITY
            *       HomeNetwork     Infra  6     130 Mbit/s  85      ████  WPA2
                    Voisin-Box      Infra  11    54 Mbit/s   55      ███   WPA2
                    OpenWifi        Infra  3     54 Mbit/s   30      ██    --
            """;

        // Act
        var networks = WifiService.ParseNetworksOutput(output);

        // Assert
        networks.Should().HaveCount(3);

        var home = networks.First(n => n.Ssid == "HomeNetwork");
        home.InUse.Should().BeTrue();
        home.Signal.Should().Be(85);
        home.Bars.Should().Be(4);
        home.Security.Should().Be("WPA2");

        var voisin = networks.First(n => n.Ssid == "Voisin-Box");
        voisin.InUse.Should().BeFalse();
        voisin.Signal.Should().Be(55);
        voisin.Security.Should().Be("WPA2");

        var open = networks.First(n => n.Ssid == "OpenWifi");
        open.Security.Should().Be("--");
    }

    [Fact]
    public void ParseNetworksOutput_WithEmptyOutput_ShouldReturnEmptyList()
    {
        // Arrange
        var output = "";

        // Act
        var networks = WifiService.ParseNetworksOutput(output);

        // Assert
        networks.Should().BeEmpty();
    }

    [Fact]
    public void ParseNetworksOutput_WithHeaderOnly_ShouldReturnEmptyList()
    {
        // Arrange
        var output = "IN-USE  SSID  MODE  CHAN  RATE  SIGNAL  BARS  SECURITY";

        // Act
        var networks = WifiService.ParseNetworksOutput(output);

        // Assert
        networks.Should().BeEmpty();
    }

    [Fact]
    public void ParseNetworksOutput_ShouldDeduplicateBySsid()
    {
        // Arrange - Même SSID apparaît deux fois (réseau dual-band)
        var output = """
            IN-USE  SSID         MODE   CHAN  RATE        SIGNAL  BARS  SECURITY
                    HomeNetwork  Infra  6     130 Mbit/s  85      ████  WPA2
                    HomeNetwork  Infra  36    300 Mbit/s  75      ███   WPA2
            """;

        // Act
        var networks = WifiService.ParseNetworksOutput(output);

        // Assert - Dédoublonnage par SSID, garde le meilleur signal
        networks.Should().HaveCount(1);
        networks[0].Ssid.Should().Be("HomeNetwork");
        networks[0].Signal.Should().Be(85);
    }

    [Fact]
    public void ParseNetworksOutput_ActiveNetworkShouldBeFirst()
    {
        // Arrange
        var output = """
            IN-USE  SSID         MODE   CHAN  RATE        SIGNAL  BARS  SECURITY
                    Voisin       Infra  11    54 Mbit/s   90      ████  WPA2
            *       HomeNetwork  Infra  6     130 Mbit/s  85      ████  WPA2
            """;

        // Act
        var networks = WifiService.ParseNetworksOutput(output);

        // Assert - Le réseau actif (InUse) doit être en premier
        networks.Should().HaveCount(2);
        networks[0].InUse.Should().BeTrue();
        networks[0].Ssid.Should().Be("HomeNetwork");
    }

    [Fact]
    public void ParseNetworksOutput_ShouldInitializeWithNoSavedProfile()
    {
        // Arrange
        var output = """
            IN-USE  SSID         MODE   CHAN  RATE        SIGNAL  BARS  SECURITY
                    TestNetwork  Infra  6     130 Mbit/s  70      ███   WPA2
            """;

        // Act
        var networks = WifiService.ParseNetworksOutput(output);

        // Assert - Par défaut, pas de profil sauvegardé
        networks[0].HasSavedProfile.Should().BeFalse();
        networks[0].ConnectionUuid.Should().BeNull();
    }

    #endregion

    #region ParseConnectionsOutput Tests

    [Fact]
    public void ParseConnectionsOutput_WithValidOutput_ShouldReturnWifiConnections()
    {
        // Arrange - Sortie nmcli c typique
        var output = """
            NAME              UUID                                  TYPE             DEVICE
            HomeNetwork       aaaaaaaa-0000-0000-0000-000000000001  802-11-wireless  wlan0
            Ethernet          bbbbbbbb-0000-0000-0000-000000000002  802-3-ethernet   eth0
            F5ZVB-AP          cccccccc-0000-0000-0000-000000000003  802-11-wireless  --
            """;

        // Act
        var connections = WifiService.ParseConnectionsOutput(output);

        // Assert - Seulement les connexions WiFi (802-11-wireless)
        connections.Should().HaveCount(2);
        connections.Should().AllSatisfy(c => c.Type.Should().Contain("wireless"));

        var home = connections.First(c => c.Name == "HomeNetwork");
        home.Uuid.Should().Be("aaaaaaaa-0000-0000-0000-000000000001");
        home.Device.Should().Be("wlan0");

        var f5zvb = connections.First(c => c.Name == "F5ZVB-AP");
        f5zvb.Uuid.Should().Be("cccccccc-0000-0000-0000-000000000003");
    }

    [Fact]
    public void ParseConnectionsOutput_WithNoWifiConnections_ShouldReturnEmptyList()
    {
        // Arrange
        var output = """
            NAME        UUID                                  TYPE            DEVICE
            Ethernet    bbbbbbbb-0000-0000-0000-000000000002  802-3-ethernet  eth0
            """;

        // Act
        var connections = WifiService.ParseConnectionsOutput(output);

        // Assert
        connections.Should().BeEmpty();
    }

    [Fact]
    public void ParseConnectionsOutput_WithEmptyOutput_ShouldReturnEmptyList()
    {
        // Arrange
        var output = "";

        // Act
        var connections = WifiService.ParseConnectionsOutput(output);

        // Assert
        connections.Should().BeEmpty();
    }

    #endregion

    #region ComputeBars Tests

    [Theory]
    [InlineData(0, 0)]
    [InlineData(19, 0)]
    [InlineData(20, 1)]
    [InlineData(39, 1)]
    [InlineData(40, 2)]
    [InlineData(59, 2)]
    [InlineData(60, 3)]
    [InlineData(79, 3)]
    [InlineData(80, 4)]
    [InlineData(100, 4)]
    public void ComputeBars_ShouldReturnCorrectBars(int signal, int expectedBars)
    {
        // Act
        var bars = WifiService.ComputeBars(signal);

        // Assert
        bars.Should().Be(expectedBars);
    }

    #endregion
}
