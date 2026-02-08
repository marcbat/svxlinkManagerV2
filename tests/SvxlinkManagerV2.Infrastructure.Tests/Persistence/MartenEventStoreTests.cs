using FluentAssertions;
using Marten;
using SvxlinkManagerV2.Domain.Aggregates.Test;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests d'intégration pour valider l'Event Sourcing avec Marten et PostgreSQL.
/// Partage le container PostgreSQL avec tous les autres tests via la collection "PostgresIntegration".
/// Chaque test crée sa propre session et utilise des IDs uniques pour l'isolation.
/// </summary>
[Trait("Category", "Integration")]
[Collection("PostgresIntegration")]
public class MartenEventStoreTests : IDisposable
{
    private readonly PostgresContainerFixture _fixture;
    private readonly IDocumentSession _session;
    private readonly MartenEventStoreRepository _repository;

    public MartenEventStoreTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
        
        // Chaque test obtient sa propre session pour l'isolation
        _session = _fixture.DocumentStore.LightweightSession();
        _repository = new MartenEventStoreRepository(_session);
    }

    /// <summary>
    /// Nettoie la session après chaque test
    /// </summary>
    public void Dispose()
    {
        _session?.Dispose();
    }

    [Fact]
    public async Task AppendEvents_ShouldPersistEventsToStream()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var aggregate = TestAggregate.Create(aggregateId, "TestValue");
        var events = aggregate.DomainEvents.ToArray();

        // Act
        await _repository.AppendEventsAsync<TestAggregate>(aggregateId, events);
        await _repository.SaveChangesAsync();

        // Assert
        var reloadedAggregate = await _repository.LoadAggregateAsync<TestAggregate>(aggregateId);
        reloadedAggregate.Should().NotBeNull();
        reloadedAggregate!.Id.Should().Be(aggregateId);
        reloadedAggregate.Value.Should().Be("TestValue");
    }

    [Fact]
    public async Task LoadAggregate_ShouldRehydrateFromEventStream()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var aggregate = TestAggregate.Create(aggregateId, "InitialValue");
        
        // Sauvegarder la création
        await _repository.AppendEventsAsync<TestAggregate>(aggregateId, aggregate.DomainEvents.ToArray());
        await _repository.SaveChangesAsync();
        
        // Créer une nouvelle session pour simuler une rehydratation complète
        var newSession = _fixture.DocumentStore.LightweightSession();
        var newRepository = new MartenEventStoreRepository(newSession);

        // Act - Recharger l'aggregate
        var reloadedAggregate = await newRepository.LoadAggregateAsync<TestAggregate>(aggregateId);

        // Assert
        reloadedAggregate.Should().NotBeNull();
        reloadedAggregate!.Id.Should().Be(aggregateId);
        reloadedAggregate.Value.Should().Be("InitialValue");
    }

    [Fact]
    public async Task UpdateAggregate_ShouldAppendNewEventsToStream()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var aggregate = TestAggregate.Create(aggregateId, "InitialValue");
        
        // Sauvegarder la création
        await _repository!.AppendEventsAsync<TestAggregate>(aggregateId, aggregate.DomainEvents.ToArray());
        await _repository.SaveChangesAsync();
        aggregate.ClearDomainEvents();
        
        // Act - Mettre à jour la valeur
        aggregate.UpdateValue("UpdatedValue");
        await _repository.AppendEventsAsync<TestAggregate>(aggregateId, aggregate.DomainEvents.ToArray());
        await _repository.SaveChangesAsync();

        // Recharger depuis le stream
        var newSession = _fixture.DocumentStore.LightweightSession();
        var newRepository = new MartenEventStoreRepository(newSession);
        var reloadedAggregate = await newRepository.LoadAggregateAsync<TestAggregate>(aggregateId);
        
        newSession.Dispose();

        // Assert
        reloadedAggregate.Should().NotBeNull();
        reloadedAggregate!.Id.Should().Be(aggregateId);
        reloadedAggregate.Value.Should().Be("UpdatedValue");
    }

    [Fact]
    public async Task EventSourcing_CompleteWorkflow_ShouldReconstructStateFromEvents()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();

        // Act - Cycle complet : Créer, Modifier plusieurs fois, Recharger
        var aggregate = TestAggregate.Create(aggregateId, "Value1");
        await _repository!.AppendEventsAsync<TestAggregate>(aggregateId, aggregate.DomainEvents.ToArray());
        await _repository.SaveChangesAsync();
        aggregate.ClearDomainEvents();

        aggregate.UpdateValue("Value2");
        await _repository.AppendEventsAsync<TestAggregate>(aggregateId, aggregate.DomainEvents.ToArray());
        await _repository.SaveChangesAsync();
        aggregate.ClearDomainEvents();

        aggregate.UpdateValue("Value3");
        await _repository.AppendEventsAsync<TestAggregate>(aggregateId, aggregate.DomainEvents.ToArray());
        await _repository.SaveChangesAsync();

        // Recharger depuis le stream complet (3 événements)
        var newSession = _fixture.DocumentStore.LightweightSession();
        var newRepository = new MartenEventStoreRepository(newSession);
        var reloadedAggregate = await newRepository.LoadAggregateAsync<TestAggregate>(aggregateId);
        
        newSession.Dispose();

        // Assert
        reloadedAggregate.Should().NotBeNull();
        reloadedAggregate!.Id.Should().Be(aggregateId);
        reloadedAggregate.Value.Should().Be("Value3", "Les événements Apply() doivent reconstruire l'état final");
    }

    [Fact]
    public async Task LoadAggregate_NonExistingId_ShouldReturnNull()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository.LoadAggregateAsync<TestAggregate>(nonExistingId);

        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task MultipleTests_ShareSameContainer_ShouldBeIsolated()
    {
        // Arrange - Vérifier qu'on peut créer plusieurs aggregates sans conflit
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        
        var aggregate1 = TestAggregate.Create(id1, "Aggregate1");
        var aggregate2 = TestAggregate.Create(id2, "Aggregate2");

        // Act
        await _repository.AppendEventsAsync<TestAggregate>(id1, aggregate1.DomainEvents.ToArray());
        await _repository.AppendEventsAsync<TestAggregate>(id2, aggregate2.DomainEvents.ToArray());
        await _repository.SaveChangesAsync();

        // Recharger les deux aggregates
        var reloaded1 = await _repository.LoadAggregateAsync<TestAggregate>(id1);
        var reloaded2 = await _repository.LoadAggregateAsync<TestAggregate>(id2);

        // Assert - Les deux aggregates sont isolés et indépendants
        reloaded1.Should().NotBeNull();
        reloaded1!.Value.Should().Be("Aggregate1");
        
        reloaded2.Should().NotBeNull();
        reloaded2!.Value.Should().Be("Aggregate2");
    }
}
