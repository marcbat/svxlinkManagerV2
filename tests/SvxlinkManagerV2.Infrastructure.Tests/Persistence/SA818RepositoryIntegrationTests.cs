using FluentAssertions;
using LanguageExt.UnitTesting;
using Marten;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Infrastructure.Persistence.Projections;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests d'intégration pour SA818Repository avec Event Sourcing et PostgreSQL.
/// Utilise Testcontainers pour créer un conteneur PostgreSQL temporaire.
/// </summary>
[Trait("Category", "Integration")]
public class SA818RepositoryIntegrationTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private IDocumentSession _session = null!;
    private SA818Repository _repository = null!;

    public SA818RepositoryIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Initialise une nouvelle session et nettoie les données AVANT chaque test pour l'isolation
    /// </summary>
    public async Task InitializeAsync()
    {
        // Créer une nouvelle session pour ce test
        _session = _fixture.DocumentStore.LightweightSession();
        _repository = new SA818Repository(_session);

        // Nettoyer toutes les projections SA818 des tests précédents
        _session.DeleteWhere<SA818Projection>(x => true);
        await _session.SaveChangesAsync();
    }

    /// <summary>
    /// Nettoie la session après chaque test
    /// </summary>
    public Task DisposeAsync()
    {
        _session?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistSA818EventsToStream()
    {
        // Arrange
        var sa818 = SA818Aggregate.Create(
                volume: 4,
                squelch: 4,
                bandwidth: SA818Bandwidth.Wide25kHz,
                preEmph: false,
                highPass: false,
                lowPass: false)
            .Match(
                Succ: s => s,
                Fail: _ => throw new InvalidOperationException("Failed to create SA818"));

        // Act
        var saveResult = await _repository.SaveAsync(sa818, CancellationToken.None);

        // Assert
        saveResult.ShouldBeSuccess();

        // Recharger depuis le stream pour vérifier
        var reloadResult = await _repository.GetAsync(CancellationToken.None);
        reloadResult.ShouldBeSuccess(reloaded =>
        {
            reloaded.Id.Should().Be(SA818Aggregate.FixedId);
            reloaded.Volume.Should().Be(4);
            reloaded.Squelch.Should().Be(4);
            reloaded.Bandwidth.Should().Be(SA818Bandwidth.Wide25kHz);
            reloaded.PreEmph.Should().BeFalse();
            reloaded.HighPass.Should().BeFalse();
            reloaded.LowPass.Should().BeFalse();
        });
    }

    [Fact]
    public async Task GetAsync_ShouldRehydrateSA818FromEventStream()
    {
        // Arrange
        var sa818 = SA818Aggregate.Create(
                volume: 6,
                squelch: 3,
                bandwidth: SA818Bandwidth.Narrow12_5kHz,
                preEmph: true,
                highPass: true,
                lowPass: false)
            .Match(
                Succ: s => s,
                Fail: _ => throw new InvalidOperationException("Failed to create SA818"));

        await _repository.SaveAsync(sa818, CancellationToken.None);

        // Créer une nouvelle session pour simuler une rehydratation complète
        var newSession = _fixture.DocumentStore.LightweightSession();
        var newRepository = new SA818Repository(newSession);

        // Act
        var result = await newRepository.GetAsync(CancellationToken.None);

        // Assert
        result.ShouldBeSuccess(reloaded =>
        {
            reloaded.Id.Should().Be(SA818Aggregate.FixedId);
            reloaded.Volume.Should().Be(6);
            reloaded.Squelch.Should().Be(3);
            reloaded.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
            reloaded.PreEmph.Should().BeTrue();
            reloaded.HighPass.Should().BeTrue();
            reloaded.LowPass.Should().BeFalse();
        });

        newSession.Dispose();
    }

    [Fact]
    public async Task GetAsync_WhenSA818NotFound_ShouldReturnNotFoundError()
    {
        // Arrange - Ne créer AUCUN SA818

        // Act
        var result = await _repository.GetAsync(CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().NotBeEmpty();
            errors.Head().Code.Should().Contain("NOT_FOUND");
        });
    }

    [Fact]
    public async Task GetProjectionAsync_ShouldReturnProjectionAfterSave()
    {
        // Arrange
        var sa818 = SA818Aggregate.Create(
                volume: 5,
                squelch: 5,
                bandwidth: SA818Bandwidth.Wide25kHz,
                preEmph: false,
                highPass: false,
                lowPass: true)
            .Match(
                Succ: s => s,
                Fail: _ => throw new InvalidOperationException("Failed to create SA818"));

        await _repository.SaveAsync(sa818, CancellationToken.None);

        // Act
        var projection = await _repository.GetProjectionAsync(CancellationToken.None);

        // Assert
        projection.Should().NotBeNull();
        projection!.Id.Should().Be(SA818Aggregate.FixedId);
        projection.Volume.Should().Be(5);
        projection.Squelch.Should().Be(5);
        projection.Bandwidth.Should().Be(SA818Bandwidth.Wide25kHz);
        projection.PreEmph.Should().BeFalse();
        projection.HighPass.Should().BeFalse();
        projection.LowPass.Should().BeTrue();
    }

    [Fact]
    public async Task GetProjectionAsync_WhenSA818NotFound_ShouldReturnNull()
    {
        // Arrange - Ne créer AUCUN SA818

        // Act
        var projection = await _repository.GetProjectionAsync(CancellationToken.None);

        // Assert
        projection.Should().BeNull();
    }

    [Fact]
    public async Task UpdateConfiguration_ShouldAppendNewEventAndUpdateProjection()
    {
        // Arrange - Créer SA818 initial
        var sa818 = SA818Aggregate.Create(
                volume: 4,
                squelch: 4,
                bandwidth: SA818Bandwidth.Wide25kHz,
                preEmph: false,
                highPass: false,
                lowPass: false)
            .Match(
                Succ: s => s,
                Fail: _ => throw new InvalidOperationException("Failed to create SA818"));

        await _repository.SaveAsync(sa818, CancellationToken.None);

        // Recharger l'aggregate
        var reloadResult = await _repository.GetAsync(CancellationToken.None);
        var reloadedSa818 = reloadResult.Match(
            Succ: s => s,
            Fail: _ => throw new InvalidOperationException("Failed to reload SA818"));

        // Act - Mettre à jour la configuration
        var updateResult = reloadedSa818.UpdateConfiguration(
            volume: 8,
            squelch: 2,
            bandwidth: SA818Bandwidth.Narrow12_5kHz,
            preEmph: true,
            highPass: true,
            lowPass: true);

        updateResult.ShouldBeSuccess();
        await _repository.SaveAsync(reloadedSa818, CancellationToken.None);

        // Assert - Vérifier projection mise à jour
        var projection = await _repository.GetProjectionAsync(CancellationToken.None);
        projection.Should().NotBeNull();
        projection!.Volume.Should().Be(8);
        projection.Squelch.Should().Be(2);
        projection.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
        projection.PreEmph.Should().BeTrue();
        projection.HighPass.Should().BeTrue();
        projection.LowPass.Should().BeTrue();

        // Assert - Vérifier aggregate rehydraté
        var finalReloadResult = await _repository.GetAsync(CancellationToken.None);
        finalReloadResult.ShouldBeSuccess(finalSa818 =>
        {
            finalSa818.Volume.Should().Be(8);
            finalSa818.Squelch.Should().Be(2);
            finalSa818.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
            finalSa818.PreEmph.Should().BeTrue();
            finalSa818.HighPass.Should().BeTrue();
            finalSa818.LowPass.Should().BeTrue();
        });
    }

    [Fact]
    public async Task SaveAsync_WithInvalidAggregateId_ShouldReturnValidationError()
    {
        // Arrange - Créer un aggregate SA818 avec un ID différent du fixe (simulation d'erreur)
        var sa818 = SA818Aggregate.Create()
            .Match(
                Succ: s => s,
                Fail: _ => throw new InvalidOperationException("Failed to create SA818"));

        // Forcer un ID invalide via réflexion pour tester la validation
        var idProperty = typeof(SA818Aggregate).GetProperty("Id");
        if (idProperty != null)
        {
            idProperty.SetValue(sa818, Guid.NewGuid());

            // Act
            var saveResult = await _repository.SaveAsync(sa818, CancellationToken.None);

            // Assert
            saveResult.ShouldBeFail(errors =>
            {
                errors.Should().NotBeEmpty();
                errors.Head().Code.Should().Be("INVALID_AGGREGATE_ID");
            });
        }
    }
}
