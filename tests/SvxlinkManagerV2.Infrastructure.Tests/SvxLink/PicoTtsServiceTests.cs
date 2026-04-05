using FluentAssertions;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.SvxLink;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests unitaires pour PicoTtsService.
/// Les tests qui invoquent réellement pico2wave sont conditionnels à la présence de l'exécutable
/// (environnement Orange Pi / Docker uniquement).
/// </summary>
public class PicoTtsServiceTests
{
    private readonly ILogger<PicoTtsService> _logger;

    public PicoTtsServiceTests()
    {
        _logger = Substitute.For<ILogger<PicoTtsService>>();
    }

    // -------------------------------------------------------------------------
    // Sanitization du texte
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Hello world", "Hello world")]
    [InlineData("  espaces  ", "espaces")]
    [InlineData("ligne1\nligne2", "ligne1 ligne2")]
    [InlineData("tabulation\there", "tabulation here")]
    [InlineData("retour\rchariot", "retour chariot")]
    [InlineData("multi   espaces", "multi espaces")]
    public void SanitizeText_ShouldRemoveDangerousCharacters(string input, string expected)
    {
        var result = PicoTtsService.SanitizeText(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void SanitizeText_WithNullCharacter_ShouldReplaceWithSpace()
    {
        var input = "nul\0char";
        var result = PicoTtsService.SanitizeText(input);
        result.Should().Be("nul char");
    }

    [Fact]
    public void SanitizeText_WithControlCharacters_ShouldReplaceWithSpace()
    {
        // Plage complète des caractères de contrôle (0x00-0x1F)
        var input = string.Concat(Enumerable.Range(0, 32).Select(i => (char)i)) + "fin";
        var result = PicoTtsService.SanitizeText(input);
        result.Should().NotContainAny(Enumerable.Range(0, 32).Select(i => ((char)i).ToString()));
        result.Should().EndWith("fin");
    }

    // -------------------------------------------------------------------------
    // Validation des paramètres
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateWavAsync_WithEmptyText_ShouldReturnFailure()
    {
        var service = new PicoTtsService(_logger);

        var result = await service.GenerateWavAsync(string.Empty, "/tmp/test.wav");

        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWavAsync_WithWhitespaceText_ShouldReturnFailure()
    {
        var service = new PicoTtsService(_logger);

        var result = await service.GenerateWavAsync("   ", "/tmp/test.wav");

        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWavAsync_WithEmptyOutputPath_ShouldReturnFailure()
    {
        var service = new PicoTtsService(_logger);

        var result = await service.GenerateWavAsync("Texte valide", string.Empty);

        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWavAsync_WithWhitespaceOutputPath_ShouldReturnFailure()
    {
        var service = new PicoTtsService(_logger);

        var result = await service.GenerateWavAsync("Texte valide", "   ");

        result.IsFail.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Comportement quand pico2wave n'est pas installé
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateWavAsync_WithValidArgs_ShouldNotThrowUnhandledException()
    {
        // Ce test vérifie que l'appel retourne toujours un Validation (Success ou Failure),
        // sans lever d'exception non gérée, que pico2wave soit installé ou non.
        var service = new PicoTtsService(_logger);
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.wav");

        try
        {
            // Act : l'appel ne doit pas lever d'exception
            var act = async () => await service.GenerateWavAsync("Test", outputPath);
            await act.Should().NotThrowAsync();
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }
}
