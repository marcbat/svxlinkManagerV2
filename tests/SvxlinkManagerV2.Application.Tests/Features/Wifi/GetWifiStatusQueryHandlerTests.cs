using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Wifi;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Wifi;

namespace SvxlinkManagerV2.Application.Tests.Features.Wifi;

/// <summary>
/// Tests unitaires pour GetWifiStatusQueryHandler.
/// </summary>
public class GetWifiStatusQueryHandlerTests
{
    private readonly IWifiService _wifiService;
    private readonly GetWifiStatusQueryHandler _handler;

    public GetWifiStatusQueryHandlerTests()
    {
        _wifiService = Substitute.For<IWifiService>();
        _handler = new GetWifiStatusQueryHandler(_wifiService);
    }

    [Fact]
    public async Task Handle_WhenNetworksAndConnectionsAvailable_ShouldReturnStatus()
    {
        // Arrange
        var networks = new List<WifiNetwork>
        {
            new WifiNetwork(true, "HomeNetwork", "Infra", "6", "130 Mbit/s", 85, 4, "WPA2", false, null),
            new WifiNetwork(false, "Voisin", "Infra", "11", "54 Mbit/s", 55, 3, "WPA2", false, null)
        }.AsReadOnly();

        var connections = new List<WifiConnection>
        {
            new WifiConnection("HomeNetwork", "uuid-1111", "802-11-wireless", "wlan0")
        }.AsReadOnly();

        _wifiService.ScanNetworksAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, IReadOnlyList<WifiNetwork>>.Success(networks));
        _wifiService.GetSavedConnectionsAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, IReadOnlyList<WifiConnection>>.Success(connections));

        // Act
        var result = await _handler.Handle(new GetWifiStatusQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: status =>
            {
                status.IsConnected.Should().BeTrue();
                status.ConnectedSsid.Should().Be("HomeNetwork");
                status.Signal.Should().Be(85);
                status.Networks.Should().HaveCount(2);

                // Vérifier la fusion avec le profil sauvegardé
                var homeNetwork = status.Networks.First(n => n.Ssid == "HomeNetwork");
                homeNetwork.HasSavedProfile.Should().BeTrue();
                homeNetwork.ConnectionUuid.Should().Be("uuid-1111");
            },
            Fail: _ => Assert.Fail("Expected success"));
    }

    [Fact]
    public async Task Handle_WhenNoActiveConnection_ShouldReturnDisconnectedStatus()
    {
        // Arrange
        var networks = new List<WifiNetwork>
        {
            new WifiNetwork(false, "Voisin", "Infra", "11", "54 Mbit/s", 55, 3, "WPA2", false, null)
        }.AsReadOnly();

        _wifiService.ScanNetworksAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, IReadOnlyList<WifiNetwork>>.Success(networks));
        _wifiService.GetSavedConnectionsAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, IReadOnlyList<WifiConnection>>.Success(
                (IReadOnlyList<WifiConnection>)new List<WifiConnection>().AsReadOnly()));

        // Act
        var result = await _handler.Handle(new GetWifiStatusQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: status =>
            {
                status.IsConnected.Should().BeFalse();
                status.ConnectedSsid.Should().BeNull();
                status.Signal.Should().BeNull();
            },
            Fail: _ => Assert.Fail("Expected success"));
    }

    [Fact]
    public async Task Handle_WhenScanFails_ShouldReturnFailure()
    {
        // Arrange
        _wifiService.ScanNetworksAsync(Arg.Any<CancellationToken>())
            .Returns(Error.Validation("WIFI_COMMAND_FAILED", "nmcli introuvable").ToFailure<IReadOnlyList<WifiNetwork>>());

        // Act
        var result = await _handler.Handle(new GetWifiStatusQuery(), CancellationToken.None);

        // Assert
        result.IsFail.Should().BeTrue();
        await _wifiService.DidNotReceive().GetSavedConnectionsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenGetConnectionsFails_ShouldReturnFailure()
    {
        // Arrange
        var networks = new List<WifiNetwork>
        {
            new WifiNetwork(false, "Test", "Infra", "6", "54 Mbit/s", 60, 3, "WPA2", false, null)
        }.AsReadOnly();

        _wifiService.ScanNetworksAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, IReadOnlyList<WifiNetwork>>.Success(networks));

        _wifiService.GetSavedConnectionsAsync(Arg.Any<CancellationToken>())
            .Returns(Error.Validation("WIFI_COMMAND_FAILED", "Erreur nmcli").ToFailure<IReadOnlyList<WifiConnection>>());

        // Act
        var result = await _handler.Handle(new GetWifiStatusQuery(), CancellationToken.None);

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNetworkMatchesSavedConnection_ShouldEnrichWithUuid()
    {
        // Arrange
        var networks = new List<WifiNetwork>
        {
            new WifiNetwork(false, "F5ZVB-AP", "Infra", "1", "300 Mbit/s", 72, 3, "WPA2", false, null)
        }.AsReadOnly();

        var connections = new List<WifiConnection>
        {
            new WifiConnection("F5ZVB-AP", "uuid-f5zvb", "802-11-wireless", "")
        }.AsReadOnly();

        _wifiService.ScanNetworksAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, IReadOnlyList<WifiNetwork>>.Success(networks));
        _wifiService.GetSavedConnectionsAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, IReadOnlyList<WifiConnection>>.Success(connections));

        // Act
        var result = await _handler.Handle(new GetWifiStatusQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: status =>
            {
                var network = status.Networks.First();
                network.HasSavedProfile.Should().BeTrue();
                network.ConnectionUuid.Should().Be("uuid-f5zvb");
            },
            Fail: _ => Assert.Fail("Expected success"));
    }
}
