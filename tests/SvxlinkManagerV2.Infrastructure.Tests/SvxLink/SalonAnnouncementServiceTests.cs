using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using static LanguageExt.Prelude;
using LangExtError = LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests unitaires pour SalonAnnouncementService.
/// Valide la génération de l'annonce TTS et le nettoyage.
/// </summary>
public class SalonAnnouncementServiceTests : IDisposable
{
    private readonly ITtsService _ttsService;
    private readonly ILogger<SalonAnnouncementService> _logger;
    private readonly string _testDeployDirectory;
    private readonly SalonAnnouncementService _service;

    public SalonAnnouncementServiceTests()
    {
        _ttsService = Substitute.For<ITtsService>();
        _logger = Substitute.For<ILogger<SalonAnnouncementService>>();
        _testDeployDirectory = Path.Combine(Path.GetTempPath(), $"salon-announce-tests-{Guid.NewGuid()}");
        _service = new SalonAnnouncementService(_ttsService, _logger, new[] { _testDeployDirectory });
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDeployDirectory))
            Directory.Delete(_testDeployDirectory, recursive: true);
    }

    [Fact]
    public async Task GenerateAsync_WithValidSalonName_ShouldReturnSuccess()
    {
        // Arrange
        var salonName = "Salon Test";
        var expectedAnnouncementText = $"Bienvenue sur le {salonName}";
        _ttsService.GenerateWavAsync(expectedAnnouncementText, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // Create the temp file so Copy succeeds
                var path = callInfo.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
                return path;
            });

        // Act
        var result = await _service.GenerateAsync(salonName);

        // Assert
        result.ShouldBeSuccess();
        await _ttsService.Received(1).GenerateWavAsync(expectedAnnouncementText, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_ShouldCreateDirectoryIfNotExists()
    {
        // Arrange
        var salonName = "Salon Test";
        var expectedAnnouncementText = $"Bienvenue sur le {salonName}";
        _ttsService.GenerateWavAsync(expectedAnnouncementText, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var path = callInfo.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
                return path;
            });

        Directory.Exists(_testDeployDirectory).Should().BeFalse();

        // Act
        await _service.GenerateAsync(salonName);

        // Assert
        Directory.Exists(_testDeployDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_WhenTtsFails_ShouldReturnFailure()
    {
        // Arrange
        var salonName = "Salon Test";
        var expectedAnnouncementText = $"Bienvenue sur le {salonName}";
        _ttsService.GenerateWavAsync(expectedAnnouncementText, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Validation<LangExtError, string>.Fail(
                    Seq1(LangExtError.New("pico2wave a échoué")))));

        // Act
        var result = await _service.GenerateAsync(salonName);

        // Assert
        result.ShouldBeFail();
    }

    [Fact]
    public async Task CleanupAsync_WhenFileExists_ShouldDeleteFile()
    {
        // Arrange
        Directory.CreateDirectory(_testDeployDirectory);
        var filePath = Path.Combine(_testDeployDirectory, "Name.wav");
        await File.WriteAllBytesAsync(filePath, new byte[] { 1, 2, 3 });
        File.Exists(filePath).Should().BeTrue();

        // Act
        var result = await _service.CleanupAsync();

        // Assert
        result.ShouldBeSuccess();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task CleanupAsync_WhenFileDoesNotExist_ShouldReturnSuccess()
    {
        // Act
        var result = await _service.CleanupAsync();

        // Assert
        result.ShouldBeSuccess();
    }
}
