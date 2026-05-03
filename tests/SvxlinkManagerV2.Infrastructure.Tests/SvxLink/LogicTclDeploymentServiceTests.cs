using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.SvxLink;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests unitaires pour LogicTclDeploymentService.
/// Valide le déploiement du Logic.tcl (ressource embarquée) vers events.d/local/.
/// </summary>
public class LogicTclDeploymentServiceTests : IDisposable
{
    private readonly LogicTclDeploymentService _service;
    private readonly ILogger<LogicTclDeploymentService> _logger;
    private readonly string _testTargetDirectory;

    public LogicTclDeploymentServiceTests()
    {
        _logger = Substitute.For<ILogger<LogicTclDeploymentService>>();
        _testTargetDirectory = Path.Combine(Path.GetTempPath(), $"svxlink-logictcl-test-{Guid.NewGuid()}");
        _service = new LogicTclDeploymentService(_logger, new[] { _testTargetDirectory });
    }

    [Fact]
    public async Task DeployAsync_ShouldCreateTargetDirectoryIfNotExists()
    {
        // Arrange — répertoire non créé au préalable
        Directory.Exists(_testTargetDirectory).Should().BeFalse();

        // Act
        var result = await _service.DeployAsync();

        // Assert
        result.ShouldBeSuccess();
        Directory.Exists(_testTargetDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task DeployAsync_ShouldCreateLogicTclFile()
    {
        // Act
        var result = await _service.DeployAsync();

        // Assert
        result.ShouldBeSuccess();
        var targetPath = Path.Combine(_testTargetDirectory, "Logic.tcl");
        File.Exists(targetPath).Should().BeTrue();
    }

    [Fact]
    public async Task DeployAsync_ShouldWriteStartupProcAndCommand398Content()
    {
        // Act
        await _service.DeployAsync();

        // Assert — le fichier doit contenir proc startup {} (vide) et la commande 398 pour jouer Name.wav
        var targetPath = Path.Combine(_testTargetDirectory, "Logic.tcl");
        var content = await File.ReadAllTextAsync(targetPath);

        content.Should().Contain("proc startup {}");
        content.Should().Contain("Name.wav");
        content.Should().Contain("playMsg");
        content.Should().Contain("398",
            "la commande 398 déclenche l'annonce de connexion réussie depuis .NET");
    }

    [Fact]
    public async Task DeployAsync_ShouldNotLeaveTemporaryFile()
    {
        // Act
        await _service.DeployAsync();

        // Assert — aucun fichier .tmp résiduel
        var tmpPath = Path.Combine(_testTargetDirectory, "Logic.tcl.tmp");
        File.Exists(tmpPath).Should().BeFalse("le fichier temporaire doit être supprimé après le rename");
    }

    [Fact]
    public async Task DeployAsync_WhenCalledTwice_ShouldReplaceIdempotently()
    {
        // Act — deux déploiements successifs
        var result1 = await _service.DeployAsync();
        var result2 = await _service.DeployAsync();

        // Assert — les deux réussissent et le fichier final est valide
        result1.ShouldBeSuccess();
        result2.ShouldBeSuccess();
        var targetPath = Path.Combine(_testTargetDirectory, "Logic.tcl");
        File.Exists(targetPath).Should().BeTrue();
    }

    [Fact]
    public async Task DeployAsync_ContentShouldBeCompatibleWithSvxLink1909()
    {
        // Act
        await _service.DeployAsync();

        // Assert — vérifier la structure TCL compatible SVXLink 19.09.2
        var targetPath = Path.Combine(_testTargetDirectory, "Logic.tcl");
        var content = await File.ReadAllTextAsync(targetPath);

        content.Should().Contain("namespace eval Logic");
        content.Should().NotContain("STARTUP_ANNOUNCEMENTS",
            "ce paramètre n'existe pas dans SVXLink 19.09.2");
        content.Should().NotContain("SHORT_ANNOUNCE_FILE",
            "ces paramètres sont pour les annonces périodiques, pas le one-shot");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testTargetDirectory))
        {
            try
            {
                Directory.Delete(_testTargetDirectory, true);
            }
            catch
            {
                // Ignorer les erreurs de nettoyage
            }
        }
    }
}
