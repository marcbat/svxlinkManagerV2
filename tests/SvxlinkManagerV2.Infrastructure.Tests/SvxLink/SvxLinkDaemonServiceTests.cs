using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.SvxLink;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests pour SvxLinkDaemonMockService.
/// Ces tests valident que le mock simule correctement les opérations systemctl.
/// </summary>
public class SvxLinkDaemonMockServiceTests
{
    private readonly SvxLinkDaemonMockService _service;
    private readonly ILogger<SvxLinkDaemonMockService> _logger;

    public SvxLinkDaemonMockServiceTests()
    {
        _logger = Substitute.For<ILogger<SvxLinkDaemonMockService>>();
        _service = new SvxLinkDaemonMockService(_logger);
    }

    [Fact]
    public async Task RestartAsync_ShouldReturnSuccess()
    {
        // Act
        var result = await _service.RestartAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(unit => unit.Should().NotBeNull());
    }

    [Fact]
    public async Task RestartAsync_ShouldLogMockOperations()
    {
        // Act
        await _service.RestartAsync();

        // Assert
        _logger.Received(1).LogInformation("MOCK: Redémarrage du daemon SVXLink");
        _logger.Received(1).LogInformation("MOCK: Exécution de la commande: systemctl restart svxlink");
        _logger.Received(1).LogInformation("MOCK: Daemon SVXLink redémarré avec succès");
    }

    [Fact]
    public async Task IsRunningAsync_ShouldReturnTrue()
    {
        // Act
        var result = await _service.IsRunningAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(isRunning => isRunning.Should().BeTrue());
    }

    [Fact]
    public async Task IsRunningAsync_ShouldLogMockOperations()
    {
        // Act
        await _service.IsRunningAsync();

        // Assert
        _logger.Received(1).LogInformation("MOCK: Vérification de l'état du daemon SVXLink");
        _logger.Received(1).LogInformation("MOCK: Exécution de la commande: systemctl is-active svxlink");
        _logger.Received(1).LogInformation("MOCK: Daemon SVXLink actif (simulé)");
    }

    [Fact]
    public async Task RestartAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _service.RestartAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task IsRunningAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _service.IsRunningAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

/// <summary>
/// Tests pour SvxLinkDaemonService.
/// Ces tests valident le comportement du service réel (sans vraiment appeler systemctl).
/// Note: Les tests d'intégration complets nécessiteraient un environnement Linux avec systemctl.
/// </summary>
public class SvxLinkDaemonServiceTests
{
    private readonly ILogger<SvxLinkDaemonService> _logger;

    public SvxLinkDaemonServiceTests()
    {
        _logger = Substitute.For<ILogger<SvxLinkDaemonService>>();
    }

    [Fact]
    public void Constructor_ShouldNotThrow()
    {
        // Act
        var act = () => new SvxLinkDaemonService(_logger);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task RestartAsync_OnWindows_ShouldReturnFailure()
    {
        // Arrange
        var service = new SvxLinkDaemonService(_logger);

        // Act
        // Sur Windows, systemctl n'existe pas, donc cela devrait échouer
        var result = await service.RestartAsync();

        // Assert - Sur Windows, on s'attend à un échec car systemctl n'existe pas
        if (OperatingSystem.IsWindows())
        {
            result.IsFail.Should().BeTrue();
        }
    }

    [Fact]
    public async Task IsRunningAsync_OnWindows_ShouldReturnFailure()
    {
        // Arrange
        var service = new SvxLinkDaemonService(_logger);

        // Act
        // Sur Windows, systemctl n'existe pas, donc cela devrait échouer
        var result = await service.IsRunningAsync();

        // Assert - Sur Windows, on s'attend à un échec car systemctl n'existe pas
        if (OperatingSystem.IsWindows())
        {
            result.IsFail.Should().BeTrue();
        }
    }

    [Fact]
    public async Task RestartAsync_ShouldLogOperations()
    {
        // Arrange
        var service = new SvxLinkDaemonService(_logger);

        // Act
        await service.RestartAsync();

        // Assert
        _logger.Received(1).LogInformation("Redémarrage du daemon SVXLink");
    }

    [Fact]
    public async Task IsRunningAsync_ShouldLogOperations()
    {
        // Arrange
        var service = new SvxLinkDaemonService(_logger);

        // Act
        await service.IsRunningAsync();

        // Assert
        _logger.Received(1).LogInformation("Vérification de l'état du daemon SVXLink");
    }
}
