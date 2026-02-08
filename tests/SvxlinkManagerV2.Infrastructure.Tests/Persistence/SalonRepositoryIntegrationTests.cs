using FluentAssertions;
using LanguageExt.UnitTesting;
using Marten;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Projections;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests d'intégration pour SalonRepository avec Event Sourcing et PostgreSQL.
/// Partage le container PostgreSQL avec tous les autres tests via la collection "PostgresIntegration".
/// </summary>
[Trait("Category", "Integration")]
[Collection("PostgresIntegration")]
public class SalonRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private IDocumentSession _session = null!;
    private SalonRepository _repository = null!;

    public SalonRepositoryIntegrationTests(PostgresContainerFixture fixture)
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
        _repository = new SalonRepository(_session);

        // Nettoyer toutes les projections Salon des tests précédents
        _session.DeleteWhere<SalonProjection>(x => true);
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
    public async Task SaveAsync_ShouldPersistSalonEventsToStream()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(salonId, "Salon National France", true, false, config)
            .Match(
                Succ: s => s,
                Fail: _ => throw new InvalidOperationException("Failed to create salon"));

        // Act
        var saveResult = await _repository.SaveAsync(salon, CancellationToken.None);

        // Assert
        saveResult.ShouldBeSuccess();

        // Recharger depuis le stream pour vérifier
        var reloadResult = await _repository.GetByIdAsync(salonId, CancellationToken.None);
        reloadResult.ShouldBeSuccess(reloaded =>
        {
            reloaded.Id.Should().Be(salonId);
            reloaded.Name.Should().Be("Salon National France");
            reloaded.IsDefault.Should().BeTrue();
            reloaded.IsTemporized.Should().BeFalse();
            reloaded.IsActive.Should().BeFalse();
            reloaded.Configuration.Host.Should().Be(config.Host);
            reloaded.Configuration.Port.Should().Be(config.Port);
        });
    }

    [Fact]
    public async Task GetByIdAsync_ShouldRehydrateSalonFromEventStream()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(salonId, "Salon Test Rehydratation", false, false, config)
            .Match(
                Succ: s => s,
                Fail: _ => throw new InvalidOperationException("Failed to create salon"));

        await _repository.SaveAsync(salon, CancellationToken.None);

        // Créer une nouvelle session pour simuler une rehydratation complète
        var newSession = _fixture.DocumentStore.LightweightSession();
        var newRepository = new SalonRepository(newSession);

        // Act - Recharger l'aggregate
        var result = await newRepository.GetByIdAsync(salonId, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess(reloaded =>
        {
            reloaded.Id.Should().Be(salonId);
            reloaded.Name.Should().Be("Salon Test Rehydratation");
            reloaded.IsDeleted.Should().BeFalse();
            reloaded.Configuration.Host.Should().Be(config.Host);
        });

        newSession.Dispose();
    }

    [Fact]
    public async Task GetByIdAsync_WhenSalonNotFound_ShouldReturnError()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });
    }

    [Fact]
    public async Task Activate_ShouldAppendActivatedEventToStream()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(salonId, "Salon Activation Test", false, false, config)
            .Match(
                Succ: s => s,
                Fail: _ => throw new InvalidOperationException("Failed to create salon"));

        await _repository.SaveAsync(salon, CancellationToken.None);
        salon.ClearDomainEvents();

        // Act - Activer le salon
        salon.Activate();
        await _repository.SaveAsync(salon, CancellationToken.None);

        // Recharger depuis le stream
        var newSession = _fixture.DocumentStore.LightweightSession();
        var newRepository = new SalonRepository(newSession);
        var reloadResult = await newRepository.GetByIdAsync(salonId, CancellationToken.None);

        // Assert
        reloadResult.ShouldBeSuccess(reloaded =>
        {
            reloaded.IsActive.Should().BeTrue();
        });

        newSession.Dispose();
    }

    [Fact]
    public async Task UpdateConfiguration_ShouldAppendConfigurationUpdatedEvent()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(salonId, "Salon Config Update Test", false, false, config)
            .Match(
                Succ: s => s,
                Fail: _ => throw new InvalidOperationException("Failed to create salon"));

        await _repository.SaveAsync(salon, CancellationToken.None);
        salon.ClearDomainEvents();

        // Act - Mettre à jour la configuration
        var newConfig = CreateValidConfiguration();
        var newConfigId = Guid.NewGuid();
        var updatedConfig = new SvxLinkConfiguration(
            newConfigId,
            newConfig.Logics,
            newConfig.CfgDir,
            newConfig.CardSampleRate,
            newConfig.CardChannels,
            "ref.newhost.fr", // Nouveau host
            6300, // Nouveau port
            newConfig.Callsign,
            newConfig.AuthKey,
            newConfig.AudioCodec,
            newConfig.JitterBufferDelay,
            newConfig.SimplexCallsign,
            newConfig.Modules,
            newConfig.ShortIdentInterval,
            newConfig.LongIdentInterval,
            newConfig.ReportCtcss,
            newConfig.EventHandler,
            newConfig.DefaultLang,
            newConfig.RgrSoundDelay,
            newConfig.SoundId,
            newConfig.RxFrequency,
            newConfig.TxFrequency,
            newConfig.RxCtcss,
            newConfig.TxCtcss);

        salon.UpdateConfiguration(updatedConfig);
        await _repository.SaveAsync(salon, CancellationToken.None);

        // Recharger depuis le stream
        var newSession = _fixture.DocumentStore.LightweightSession();
        var newRepository = new SalonRepository(newSession);
        var reloadResult = await newRepository.GetByIdAsync(salonId, CancellationToken.None);

        // Assert
        reloadResult.ShouldBeSuccess(reloaded =>
        {
            reloaded.Configuration.Host.Should().Be("ref.newhost.fr");
            reloaded.Configuration.Port.Should().Be(6300);
        });

        newSession.Dispose();
    }

    [Fact]
    public async Task Delete_ShouldAppendDeletedEvent()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(salonId, "Salon Delete Test", false, false, config)
            .Match(
                Succ: s => s,
                Fail: _ => throw new InvalidOperationException("Failed to create salon"));

        await _repository.SaveAsync(salon, CancellationToken.None);
        salon.ClearDomainEvents();

        // Act - Supprimer le salon
        var deleteResult = await _repository.DeleteAsync(salonId, CancellationToken.None);

        // Assert
        deleteResult.ShouldBeSuccess();

        var reloadResult = await _repository.GetByIdAsync(salonId, CancellationToken.None);
        reloadResult.ShouldBeSuccess(reloaded =>
        {
            reloaded.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllNonDeletedSalons()
    {
        // Arrange
        var config = CreateValidConfiguration();
        
        var salon1 = SalonAggregate.Create(Guid.NewGuid(), "Salon 1", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        var salon2 = SalonAggregate.Create(Guid.NewGuid(), "Salon 2", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        var salon3 = SalonAggregate.Create(Guid.NewGuid(), "Salon 3 (Deleted)", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        await _repository.SaveAsync(salon1, CancellationToken.None);
        await _repository.SaveAsync(salon2, CancellationToken.None);
        await _repository.SaveAsync(salon3, CancellationToken.None);

        // Supprimer salon3
        salon3.ClearDomainEvents();
        salon3.Delete();
        await _repository.SaveAsync(salon3, CancellationToken.None);

        // Act
        var result = await _repository.GetAllAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Name == "Salon 1");
        result.Should().Contain(s => s.Name == "Salon 2");
        result.Should().NotContain(s => s.Name == "Salon 3 (Deleted)");
    }

    [Fact]
    public async Task GetActiveAsync_WhenActiveSalonExists_ShouldReturnIt()
    {
        // Arrange
        var config = CreateValidConfiguration();
        
        var salon1 = SalonAggregate.Create(Guid.NewGuid(), "Salon Inactif", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        var salon2 = SalonAggregate.Create(Guid.NewGuid(), "Salon Actif", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        await _repository.SaveAsync(salon1, CancellationToken.None);
        await _repository.SaveAsync(salon2, CancellationToken.None);

        // Activer salon2
        salon2.ClearDomainEvents();
        salon2.Activate();
        await _repository.SaveAsync(salon2, CancellationToken.None);

        // Act
        var result = await _repository.GetActiveAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Salon Actif");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveAsync_WhenNoActiveSalon_ShouldReturnNull()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(Guid.NewGuid(), "Salon Inactif", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        await _repository.SaveAsync(salon, CancellationToken.None);

        // Act
        var result = await _repository.GetActiveAsync(CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task EventSourcing_CompleteLifecycle_ShouldReconstructAllStates()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        
        // Création
        var salon = SalonAggregate.Create(salonId, "Salon Lifecycle Test", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        await _repository.SaveAsync(salon, CancellationToken.None);
        salon.ClearDomainEvents();

        // Activation
        salon.Activate();
        await _repository.SaveAsync(salon, CancellationToken.None);
        salon.ClearDomainEvents();

        // Désactivation
        salon.Deactivate();
        await _repository.SaveAsync(salon, CancellationToken.None);
        salon.ClearDomainEvents();

        // Mise à jour configuration
        var newConfig = CreateValidConfiguration();
        salon.UpdateConfiguration(newConfig);
        await _repository.SaveAsync(salon, CancellationToken.None);

        // Act - Recharger depuis le stream complet
        var newSession = _fixture.DocumentStore.LightweightSession();
        var newRepository = new SalonRepository(newSession);
        var result = await newRepository.GetByIdAsync(salonId, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess(reloaded =>
        {
            reloaded.Id.Should().Be(salonId);
            reloaded.Name.Should().Be("Salon Lifecycle Test");
            reloaded.IsActive.Should().BeFalse(); // Désactivé
            reloaded.IsDeleted.Should().BeFalse();
            reloaded.Configuration.Should().NotBe(config); // Configuration mise à jour
        });

        newSession.Dispose();
    }

    #region Helper Methods

    private static SvxLinkConfiguration CreateValidConfiguration()
    {
        return new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d",
            16000,
            1,
            "ref.f5kri.fr",
            5300,
            "F5ABC-L",
            "test-auth-key-123",
            "OPUS",
            0,
            "F5ABC",
            "ModuleHelp,ModuleParrot",
            60,
            60,
            "71.9",
            "/usr/share/svxlink/events.tcl",
            "fr_FR",
            0,
            Guid.NewGuid(),  // SoundId
            145.550m,        // RxFrequency
            145.550m,        // TxFrequency
            136.5m,          // RxCtcss
            136.5m);         // TxCtcss
    }

    #endregion
}
