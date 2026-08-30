using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink.InfoProviders;

/// <summary>
/// Tests unitaires pour UptimeInfoProvider.
/// </summary>
public class UptimeInfoProviderTests
{
    private readonly ISystemMetricsService _metrics;
    private readonly UptimeInfoProvider _provider;

    public UptimeInfoProviderTests()
    {
        _metrics = Substitute.For<ISystemMetricsService>();
        _provider = new UptimeInfoProvider(
            _metrics,
            Substitute.For<ILogger<UptimeInfoProvider>>());
    }

    [Fact]
    public void DtmfCode_ShouldBe305()
    {
        _provider.DtmfCode.Should().Be(305);
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        _provider.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(3, 4, 20, "La station fonctionne depuis 3 jours et 4 heures")]
    [InlineData(1, 0, 0, "La station fonctionne depuis 1 jour")]
    [InlineData(0, 2, 30, "La station fonctionne depuis 2 heures et 30 minutes")]
    [InlineData(0, 0, 5, "La station fonctionne depuis 5 minutes")]
    [InlineData(0, 0, 0, "La station fonctionne depuis moins d'une minute")]
    public async Task GetInfoTextAsync_WhenUptimeAvailable_ShouldReturnFrenchSentence(
        int days,
        int hours,
        int minutes,
        string expectedText)
    {
        var machineUptime = new TimeSpan(days, hours, minutes, 0);

        _metrics.GetUptimeAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, UptimeMetrics>.Success(
                new UptimeMetrics(machineUptime, TimeSpan.FromMinutes(1))));

        var result = await _provider.GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(text => text.Should().Be(expectedText));
    }

    [Fact]
    public async Task GetInfoTextAsync_WhenUptimeUnavailable_ShouldReturnFailure()
    {
        _metrics.GetUptimeAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, UptimeMetrics>.Fail(
                Prelude.Seq1(Error.Validation("SYSTEM_UPTIME_UNAVAILABLE", "Source absente"))));

        var result = await _provider.GetInfoTextAsync();

        result.IsFail.Should().BeTrue();
    }
}
