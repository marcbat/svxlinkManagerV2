using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Wifi;
using SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink.InfoProviders;

/// <summary>
/// Tests unitaires pour NetworkStatusInfoProvider (commande DTMF 303).
/// </summary>
public class NetworkStatusInfoProviderTests
{
    private readonly IWifiService _wifiService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NetworkStatusInfoProvider> _logger;

    public NetworkStatusInfoProviderTests()
    {
        _wifiService = Substitute.For<IWifiService>();
        _logger = Substitute.For<ILogger<NetworkStatusInfoProvider>>();

        var scopedProvider = Substitute.For<IServiceProvider>();
        scopedProvider.GetService(typeof(IWifiService)).Returns(_wifiService);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(scopedProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);
    }

    private NetworkStatusInfoProvider CreateProvider() => new(_scopeFactory, _logger);

    private static WifiNetwork Network(string ssid, int signal, bool inUse, string mode = "Infra") =>
        new(
            InUse: inUse,
            Ssid: ssid,
            Mode: mode,
            Channel: "6",
            Rate: "130 Mbit/s",
            Signal: signal,
            Bars: 4,
            Security: "WPA2",
            HasSavedProfile: true,
            ConnectionUuid: null);

    private void SetupScan(params WifiNetwork[] networks) =>
        _wifiService.ScanNetworksAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, IReadOnlyList<WifiNetwork>>.Success(networks));

    // -------------------------------------------------------------------------
    // Métadonnées du provider
    // -------------------------------------------------------------------------

    [Fact]
    public void DtmfCode_ShouldBe303()
    {
        CreateProvider().DtmfCode.Should().Be(303);
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        CreateProvider().Description.Should().NotBeNullOrWhiteSpace();
    }

    // -------------------------------------------------------------------------
    // Formatage du texte annoncé
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildInfoText_WhenNoActiveNetwork_ShouldAnnounceAbsence()
    {
        NetworkStatusInfoProvider.BuildInfoText(null)
            .Should().Be(NetworkStatusInfoProvider.NoNetworkText);
    }

    [Fact]
    public void BuildInfoText_ShouldContainModeSsidAndSignal()
    {
        var text = NetworkStatusInfoProvider.BuildInfoText(Network("HomeNetwork", 72, inUse: true));

        text.Should().Be("Le nœud est en mode client, connecté au réseau HomeNetwork. "
                       + "Le niveau de signal est de 72 pour cent, qualité bonne");
    }

    [Fact]
    public void BuildInfoText_WhenAccessPointMode_ShouldAnnounceAccessPoint()
    {
        var text = NetworkStatusInfoProvider.BuildInfoText(Network("SvxlinkAP", 90, inUse: true, mode: "Ap"));

        text.Should().Contain("mode point d'accès").And.Contain("SvxlinkAP");
    }

    [Fact]
    public void BuildInfoText_WhenSsidHidden_ShouldUseFallbackLabel()
    {
        NetworkStatusInfoProvider.BuildInfoText(Network("  ", 50, inUse: true))
            .Should().Contain("réseau masqué");
    }

    [Theory]
    [InlineData("Ap", "point d'accès")]
    [InlineData("AP", "point d'accès")]
    [InlineData("Master", "point d'accès")]
    [InlineData("Infra", "client")]
    [InlineData("", "client")]
    [InlineData(null, "client")]
    public void FormatMode_ShouldMapNmcliModeToSpokenLabel(string? mode, string expected)
    {
        NetworkStatusInfoProvider.FormatMode(mode).Should().Be(expected);
    }

    [Theory]
    [InlineData(95, "excellente")]
    [InlineData(80, "excellente")]
    [InlineData(60, "bonne")]
    [InlineData(40, "moyenne")]
    [InlineData(20, "faible")]
    [InlineData(5, "très faible")]
    public void FormatQuality_ShouldMapSignalToSpokenQuality(int signal, string expected)
    {
        NetworkStatusInfoProvider.FormatQuality(signal).Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // Récupération de l'état réseau
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetInfoTextAsync_WhenNetworkInUse_ShouldAnnounceIt()
    {
        SetupScan(
            Network("Voisin", 90, inUse: false),
            Network("HomeNetwork", 72, inUse: true));

        var result = await CreateProvider().GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        result.Match(Succ: t => t, Fail: _ => string.Empty)
            .Should().Contain("HomeNetwork").And.Contain("72 pour cent");
    }

    [Fact]
    public async Task GetInfoTextAsync_WhenNoNetworkInUse_ShouldAnnounceAbsenceInsteadOfFailing()
    {
        SetupScan(Network("Voisin", 90, inUse: false));

        var result = await CreateProvider().GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        result.Match(Succ: t => t, Fail: _ => string.Empty)
            .Should().Be(NetworkStatusInfoProvider.NoNetworkText);
    }

    [Fact]
    public async Task GetInfoTextAsync_WhenScanFails_ShouldReturnFailure()
    {
        _wifiService.ScanNetworksAsync(Arg.Any<CancellationToken>())
            .Returns(Error.Validation("WIFI_COMMAND_FAILED", "nmcli indisponible")
                .ToFailure<IReadOnlyList<WifiNetwork>>());

        var result = await CreateProvider().GetInfoTextAsync();

        result.IsFail.Should().BeTrue();
    }
}
