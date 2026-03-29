using FluentAssertions;
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
/// Tests d'intégration pour SA818InitializerHostedService avec SQLite.
/// </summary>
[Collection("PostgresIntegration")]
public class SA818InitializerHostedServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private SvxlinkDbContext _context = null!;
    private ISA818Repository _repository = null!;
    private ILogger<SA818InitializerHostedService> _logger = null!;
    private IServiceScopeFactory _scopeFactory = null!;

    public SA818InitializerHostedServiceIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repository = new SA818Repository(_context);
        _logger = Substitute.For<ILogger<SA818InitializerHostedService>>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISA818Repository)).Returns(_repository);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task StartAsync_WhenSA818DoesNotExist_ShouldCreateWithDefaultValues()
    {
        // Arrange
        var service = new SA818InitializerHostedService(_scopeFactory, _logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        var config = await _repository.GetConfigurationAsync();
        config.Should().NotBeNull();
        config!.Id.Should().Be(SA818Aggregate.FixedId);
        config.Volume.Should().Be(4);
        config.Squelch.Should().Be(4);
        config.Bandwidth.Should().Be(SA818Bandwidth.Wide25kHz);
        config.PreEmph.Should().BeFalse();
        config.HighPass.Should().BeFalse();
        config.LowPass.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_WhenSA818AlreadyExists_ShouldNotRecreate()
    {
        // Arrange - Créer un SA818 existant avec des valeurs personnalisées
        var existingSA818Result = SA818Aggregate.Create(6, 5, SA818Bandwidth.Narrow12_5kHz, true, true, true);

        await existingSA818Result.Match(
            async aggregate => await _repository.SaveAsync(aggregate),
            errors => throw new Exception("Échec création SA818 initial"));

        var service = new SA818InitializerHostedService(_scopeFactory, _logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Vérifier que le SA818 existant n'a PAS été modifié
        var config = await _repository.GetConfigurationAsync();
        config.Should().NotBeNull();
        config!.Volume.Should().Be(6);
        config.Squelch.Should().Be(5);
        config.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
        config.PreEmph.Should().BeTrue();
        config.HighPass.Should().BeTrue();
        config.LowPass.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_ShouldLogInitializationMessages()
    {
        // Arrange
        var service = new SA818InitializerHostedService(_scopeFactory, _logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Vérification existence SA818Aggregate")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
