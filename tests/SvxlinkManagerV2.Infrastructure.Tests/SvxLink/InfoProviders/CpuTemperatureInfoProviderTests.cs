using FluentAssertions;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink.InfoProviders;

/// <summary>
/// Tests unitaires pour CpuTemperatureInfoProvider.
/// </summary>
public class CpuTemperatureInfoProviderTests
{
    private readonly ILogger<CpuTemperatureInfoProvider> _logger;

    public CpuTemperatureInfoProviderTests()
    {
        _logger = Substitute.For<ILogger<CpuTemperatureInfoProvider>>();
    }

    // -------------------------------------------------------------------------
    // Métadonnées du provider
    // -------------------------------------------------------------------------

    [Fact]
    public void DtmfCode_ShouldBe301()
    {
        var provider = new CpuTemperatureInfoProvider(_logger);
        provider.DtmfCode.Should().Be(301);
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        var provider = new CpuTemperatureInfoProvider(_logger);
        provider.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DefaultThermalZonePath_ShouldBeCorrect()
    {
        CpuTemperatureInfoProvider.DefaultThermalZonePath
            .Should().Be("/sys/class/thermal/thermal_zone0/temp");
    }

    // -------------------------------------------------------------------------
    // Fichier système manquant
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetInfoTextAsync_WhenFileDoesNotExist_ShouldReturnFailure()
    {
        var nonExistentPath = $"/tmp/non_existent_thermal_{Guid.NewGuid()}";
        var provider = new CpuTemperatureInfoProvider(_logger, nonExistentPath);

        var result = await provider.GetInfoTextAsync();

        result.IsFail.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Lecture et conversion depuis un fichier temporaire
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("45000", "La température du processeur est de 45 degrés")]
    [InlineData("42500", "La température du processeur est de 42 degrés")]
    [InlineData("0", "La température du processeur est de 0 degrés")]
    [InlineData("1000", "La température du processeur est de 1 degrés")]
    [InlineData("75000\n", "La température du processeur est de 75 degrés")]
    public async Task GetInfoTextAsync_WithValidThermalFile_ShouldReturnCorrectText(
        string rawContent,
        string expectedText)
    {
        // Arrange
        var tmpFile = Path.Combine(Path.GetTempPath(), $"thermal_test_{Guid.NewGuid()}");
        await File.WriteAllTextAsync(tmpFile, rawContent);

        try
        {
            var provider = new CpuTemperatureInfoProvider(_logger, tmpFile);

            // Act
            var result = await provider.GetInfoTextAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.IfSuccess(text => text.Should().Be(expectedText));
        }
        finally
        {
            if (File.Exists(tmpFile))
                File.Delete(tmpFile);
        }
    }

    [Theory]
    [InlineData("not_a_number")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetInfoTextAsync_WithInvalidThermalContent_ShouldReturnFailure(string rawContent)
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"thermal_test_{Guid.NewGuid()}");
        await File.WriteAllTextAsync(tmpFile, rawContent);

        try
        {
            var provider = new CpuTemperatureInfoProvider(_logger, tmpFile);

            var result = await provider.GetInfoTextAsync();

            result.IsFail.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tmpFile))
                File.Delete(tmpFile);
        }
    }

    // -------------------------------------------------------------------------
    // Test conditionnel sur environnement Linux avec capteurs thermiques
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetInfoTextAsync_WithDefaultPath_WhenOnLinuxWithThermalSensor_ShouldSucceed()
    {
        // Ce test n'est pertinent que sur un vrai système Linux (Orange Pi)
        if (!File.Exists(CpuTemperatureInfoProvider.DefaultThermalZonePath))
            return; // Skip silencieux si pas de capteur thermal disponible

        var provider = new CpuTemperatureInfoProvider(_logger);

        var result = await provider.GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(text => text.Should().Contain("degrés"));
    }
}

