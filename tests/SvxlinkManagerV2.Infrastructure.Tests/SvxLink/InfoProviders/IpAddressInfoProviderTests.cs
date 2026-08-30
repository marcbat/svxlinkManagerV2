using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink.InfoProviders;

/// <summary>
/// Tests unitaires pour IpAddressInfoProvider (commande DTMF 302).
/// </summary>
public class IpAddressInfoProviderTests
{
    private readonly ILogger<IpAddressInfoProvider> _logger;

    public IpAddressInfoProviderTests()
    {
        _logger = Substitute.For<ILogger<IpAddressInfoProvider>>();
    }

    private IpAddressInfoProvider CreateProvider(params ActiveIpv4Address[] addresses) =>
        new(_logger, () => addresses);

    // -------------------------------------------------------------------------
    // Métadonnées du provider
    // -------------------------------------------------------------------------

    [Fact]
    public void DtmfCode_ShouldBe302()
    {
        CreateProvider().DtmfCode.Should().Be(302);
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        CreateProvider().Description.Should().NotBeNullOrWhiteSpace();
    }

    // -------------------------------------------------------------------------
    // Formatage phonie
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("192.168.1.42", "192 point 168 point 1 point 42")]
    [InlineData("10.0.0.1", "10 point 0 point 0 point 1")]
    [InlineData("192.168.001.042", "192 point 168 point 1 point 42")]
    public void FormatIpAddress_ShouldSeparateGroupsWithPoint(string ip, string expected)
    {
        IpAddressInfoProvider.FormatIpAddress(ip).Should().Be(expected);
    }

    [Fact]
    public void FormatIpAddress_ShouldNotContainDotCharacter()
    {
        IpAddressInfoProvider.FormatIpAddress("192.168.1.42").Should().NotContain(".");
    }

    [Fact]
    public void BuildInfoText_ShouldContainFormattedAddress()
    {
        IpAddressInfoProvider.BuildInfoText("192.168.1.42")
            .Should().Be("L'adresse IP du nœud est 192 point 168 point 1 point 42");
    }

    // -------------------------------------------------------------------------
    // Résolution de l'adresse active
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetInfoTextAsync_WhenAddressAvailable_ShouldAnnounceIt()
    {
        var provider = CreateProvider(new ActiveIpv4Address("wlan0", "192.168.1.42"));

        var result = await provider.GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        result.Match(Succ: t => t, Fail: _ => string.Empty)
            .Should().Be("L'adresse IP du nœud est 192 point 168 point 1 point 42");
    }

    [Fact]
    public async Task GetInfoTextAsync_WhenSeveralAddresses_ShouldAnnounceTheFirstOne()
    {
        var provider = CreateProvider(
            new ActiveIpv4Address("wlan0", "192.168.1.42"),
            new ActiveIpv4Address("eth0", "10.0.0.7"));

        var result = await provider.GetInfoTextAsync();

        result.Match(Succ: t => t, Fail: _ => string.Empty)
            .Should().Contain("192 point 168 point 1 point 42")
            .And.NotContain("10 point 0 point 0 point 7");
    }

    [Fact]
    public async Task GetInfoTextAsync_WhenNoAddress_ShouldAnnounceAbsenceInsteadOfFailing()
    {
        var provider = CreateProvider();

        var result = await provider.GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        result.Match(Succ: t => t, Fail: _ => string.Empty)
            .Should().Be(IpAddressInfoProvider.NoAddressText);
    }

    [Fact]
    public async Task GetInfoTextAsync_WhenResolverThrows_ShouldReturnFailure()
    {
        var provider = new IpAddressInfoProvider(_logger, () => throw new InvalidOperationException("boum"));

        var result = await provider.GetInfoTextAsync();

        result.IsFail.Should().BeTrue();
    }
}
