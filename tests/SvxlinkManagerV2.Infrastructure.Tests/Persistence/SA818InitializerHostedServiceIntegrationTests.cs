using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests d'intégration pour SA818InitializerHostedService.
/// Valide l'initialisation automatique du SA818Aggregate au démarrage avec PostgreSQL réel.
/// </summary>
[Collection("PostgresIntegration")]
public class SA818InitializerHostedServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private IDocumentSession _session = null!;
    private ISA818Repository _repository = null!;
    private ILogger<SA818InitializerHostedService> _logger = null!;
    private IServiceScopeFactory _scopeFactory = null!;

    public SA818InitializerHostedServiceIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Nettoyer la base avant chaque test
        await _fixture.DocumentStore.Advanced.Clean.CompletelyRemoveAllAsync();
        
        // Créer une session Marten pour chaque test
        _session = _fixture.DocumentStore.LightweightSession();
        
        // Créer le repository avec la session
        _repository = new SA818Repository(_session);
        
        // Créer un logger mocké
        _logger = Substitute.For<ILogger<SA818InitializerHostedService>>();
        
        // Créer un mock de IServiceScopeFactory qui retourne notre repository
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISA818Repository)).Returns(_repository);
        
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);
    }

    public async Task DisposeAsync()
    {
        await _session.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_WhenSA818DoesNotExist_ShouldCreateWithDefaultValues()
    {
        // Arrange
        var service = new SA818InitializerHostedService(_scopeFactory, _logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Vérifier que le SA818 a été créé avec les valeurs par défaut
        var config = await _repository.GetConfigurationAsync();
        config.Should().NotBeNull();
        config!.Id.Should().Be(SA818Aggregate.FixedId);
        config.Volume.Should().Be(4);
        config.Squelch.Should().Be(4);
        config.Bandwidth.Should().Be(SA818Bandwidth.Wide25kHz);
        config.PreEmph.Should().BeFalse();
        config.HighPass.Should().BeFalse();
        config.LowPass.Should().BeFalse();

        // Vérifier que les événements ont été persistés
        var events = await _session.Events.FetchStreamAsync(SA818Aggregate.FixedId);
        events.Should().HaveCount(1);
        events.First().EventType.Name.Should().Be(nameof(Domain.Aggregates.SA818.Events.SA818ConfigurationUpdatedEvent));
    }

    [Fact]
    public async Task StartAsync_WhenSA818AlreadyExists_ShouldNotRecreate()
    {
        // Arrange
        // Créer un SA818 existant avec des valeurs personnalisées
        var existingSA818Result = SA818Aggregate.Create(
            volume: 6,
            squelch: 5,
            bandwidth: SA818Bandwidth.Narrow12_5kHz,
            preEmph: true,
            highPass: true,
            lowPass: true);

        await existingSA818Result.Match(
            async aggregate => await _repository.SaveAsync(aggregate),
            errors => throw new Exception("Échec création SA818 initial"));

        await _session.SaveChangesAsync();

        var service = new SA818InitializerHostedService(_scopeFactory, _logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Vérifier que le SA818 existant n'a PAS été modifié
        var config = await _repository.GetConfigurationAsync();
        config.Should().NotBeNull();
        config!.Volume.Should().Be(6); // Valeur originale préservée
        config.Squelch.Should().Be(5);
        config.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
        config.PreEmph.Should().BeTrue();
        config.HighPass.Should().BeTrue();
        config.LowPass.Should().BeTrue();

        // Vérifier que seulement 1 événement existe (l'original)
        var events = await _session.Events.FetchStreamAsync(SA818Aggregate.FixedId);
        events.Should().HaveCount(1);
    }

    [Fact]
    public async Task StartAsync_ShouldLogInitializationMessages()
    {
        // Arrange
        var service = new SA818InitializerHostedService(_scopeFactory, _logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Vérifier que les logs d'information ont été appelés
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Vérification existence SA818Aggregate")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("SA818 initialisé avec succès")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StartAsync_WhenSA818Exists_ShouldLogSkipMessage()
    {
        // Arrange
        // Créer un SA818 existant
        var existingSA818Result = SA818Aggregate.Create();
        await existingSA818Result.Match(
            async aggregate => await _repository.SaveAsync(aggregate),
            errors => throw new Exception("Échec création SA818 initial"));

        await _session.SaveChangesAsync();

        var service = new SA818InitializerHostedService(_scopeFactory, _logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Vérifier que le log "déjà existant" a été appelé
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("SA818 déjà existant")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StopAsync_ShouldCompleteWithoutErrors()
    {
        // Arrange
        var service = new SA818InitializerHostedService(_scopeFactory, _logger);

        // Act
        var act = async () => await service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
