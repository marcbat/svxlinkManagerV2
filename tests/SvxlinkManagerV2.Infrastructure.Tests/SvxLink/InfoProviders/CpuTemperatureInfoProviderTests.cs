using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink.InfoProviders;

/// <summary>
/// Tests unitaires pour CpuTemperatureInfoProvider.
/// </summary>
public class CpuTemperatureInfoProviderTests
{
    private readonly ISystemMetricsService _metrics;
    private readonly CpuTemperatureInfoProvider _provider;

    public CpuTemperatureInfoProviderTests()
    {
        _metrics = Substitute.For<ISystemMetricsService>();
        _provider = new CpuTemperatureInfoProvider(
            _metrics,
            Substitute.For<ILogger<CpuTemperatureInfoProvider>>());
    }

    // -------------------------------------------------------------------------
    // Métadonnées du provider
    // -------------------------------------------------------------------------

    [Fact]
    public void DtmfCode_ShouldBe301()
    {
        _provider.DtmfCode.Should().Be(301);
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        _provider.Description.Should().NotBeNullOrWhiteSpace();
    }

    // -------------------------------------------------------------------------
    // Mise en forme de l'annonce
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(45d, "La température du processeur est de 45 degrés")]
    [InlineData(42.5d, "La température du processeur est de 43 degrés")]
    [InlineData(0d, "La température du processeur est de 0 degré")]
    [InlineData(1d, "La température du processeur est de 1 degré")]
    [InlineData(75d, "La température du processeur est de 75 degrés")]
    public async Task GetInfoTextAsync_WhenTemperatureAvailable_ShouldReturnFrenchSentence(
        double celsius,
        string expectedText)
    {
        _metrics.GetCpuTemperatureCelsiusAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, double>.Success(celsius));

        var result = await _provider.GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(text => text.Should().Be(expectedText));
    }

    // -------------------------------------------------------------------------
    // Métrique indisponible
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetInfoTextAsync_WhenTemperatureUnavailable_ShouldReturnFailure()
    {
        _metrics.GetCpuTemperatureCelsiusAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, double>.Fail(
                Prelude.Seq1(Error.Validation("SYSTEM_TEMPERATURE_UNAVAILABLE", "Capteur absent"))));

        var result = await _provider.GetInfoTextAsync();

        result.IsFail.Should().BeTrue();
    }
}
