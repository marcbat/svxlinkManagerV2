using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.SvxLink;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests unitaires pour SoundFileDeploymentService.
/// Valide le déploiement atomique des fichiers WAV et le nettoyage.
/// </summary>
public class SoundFileDeploymentServiceTests : IDisposable
{
    private readonly SoundFileDeploymentService _service;
    private readonly ILogger<SoundFileDeploymentService> _logger;
    private readonly string _testDeployDirectory;

    public SoundFileDeploymentServiceTests()
    {
        _logger = Substitute.For<ILogger<SoundFileDeploymentService>>();

        // Répertoire temporaire isolé pour chaque test
        _testDeployDirectory = Path.Combine(Path.GetTempPath(), $"svxlink-sound-test-{Guid.NewGuid()}");
        _service = new SoundFileDeploymentService(_logger, _testDeployDirectory);
    }

    [Fact]
    public async Task DeployAsync_WithValidSound_ShouldReturnDeployedPath()
    {
        // Arrange
        var sound = SoundTestHelpers.CreateValidAggregate(Guid.NewGuid(), "test-annonce");

        // Act
        var result = await _service.DeployAsync(sound);

        // Assert
        result.ShouldBeSuccess(path =>
        {
            path.Should().Be(Path.Combine(_testDeployDirectory, "announce.wav"));
        });
    }

    [Fact]
    public async Task DeployAsync_ShouldCreateTargetDirectoryIfNotExists()
    {
        // Arrange — répertoire non créé au préalable
        Directory.Exists(_testDeployDirectory).Should().BeFalse();
        var sound = SoundTestHelpers.CreateValidAggregate(Guid.NewGuid());

        // Act
        await _service.DeployAsync(sound);

        // Assert
        Directory.Exists(_testDeployDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task DeployAsync_ShouldWriteFileContentToDeployPath()
    {
        // Arrange
        var sound = SoundTestHelpers.CreateValidAggregate(Guid.NewGuid());

        // Act
        var result = await _service.DeployAsync(sound);

        // Assert
        result.ShouldBeSuccess(path =>
        {
            File.Exists(path).Should().BeTrue();
            var writtenContent = File.ReadAllBytes(path);
            writtenContent.Should().BeEquivalentTo(sound.FileContent);
        });
    }

    [Fact]
    public async Task DeployAsync_ShouldNotLeaveTemporaryFile()
    {
        // Arrange
        var sound = SoundTestHelpers.CreateValidAggregate(Guid.NewGuid());

        // Act
        var result = await _service.DeployAsync(sound);

        // Assert
        result.ShouldBeSuccess(path =>
        {
            var tempPath = $"{path}.tmp";
            File.Exists(tempPath).Should().BeFalse("le fichier temporaire doit être supprimé après le rename");
        });
    }

    [Fact]
    public async Task DeployAsync_WhenFileAlreadyExists_ShouldReplaceIdempotently()
    {
        // Arrange — premier déploiement
        var sound1 = SoundTestHelpers.CreateValidAggregate(Guid.NewGuid(), "son-1");
        await _service.DeployAsync(sound1);

        var sound2 = SoundTestHelpers.CreateValidAggregate(Guid.NewGuid(), "son-2");

        // Act — deuxième déploiement (remplacement)
        var result = await _service.DeployAsync(sound2);

        // Assert
        result.ShouldBeSuccess(path =>
        {
            File.Exists(path).Should().BeTrue();
            var writtenContent = File.ReadAllBytes(path);
            writtenContent.Should().BeEquivalentTo(sound2.FileContent);
        });
    }

    [Fact]
    public async Task CleanupAsync_WhenFileExists_ShouldDeleteFile()
    {
        // Arrange — déployer d'abord
        var sound = SoundTestHelpers.CreateValidAggregate(Guid.NewGuid());
        var deployResult = await _service.DeployAsync(sound);
        var deployedPath = deployResult.Match(Succ: p => p, Fail: _ => throw new Exception());
        File.Exists(deployedPath).Should().BeTrue();

        // Act
        var result = await _service.CleanupAsync();

        // Assert
        result.ShouldBeSuccess();
        File.Exists(deployedPath).Should().BeFalse();
    }

    [Fact]
    public async Task CleanupAsync_WhenFileDoesNotExist_ShouldSucceedGracefully()
    {
        // Arrange — aucun fichier déployé
        var deployPath = Path.Combine(_testDeployDirectory, "announce.wav");
        File.Exists(deployPath).Should().BeFalse();

        // Act
        var result = await _service.CleanupAsync();

        // Assert — ne doit pas échouer si le fichier est absent
        result.ShouldBeSuccess();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDeployDirectory))
        {
            try
            {
                Directory.Delete(_testDeployDirectory, true);
            }
            catch
            {
                // Ignorer les erreurs de nettoyage
            }
        }
    }
}
