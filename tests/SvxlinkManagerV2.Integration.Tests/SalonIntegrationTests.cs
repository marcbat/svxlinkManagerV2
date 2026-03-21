using FluentAssertions;
using LanguageExt.UnitTesting;
using Marten;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.CreateSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;
using SvxlinkManagerV2.Application.Features.Salons.GetSalonById;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Infrastructure.Persistence.Projections;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// Tests d'intégration validant le workflow complet Salon :
/// Command → Event Sourcing → Projection → Query
/// </summary>
[Trait("Category", "Integration")]
[Collection("IntegrationTests")]
public class SalonIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private IDocumentSession _session = null!;
    private SalonRepository _repository = null!;
    private IActiveSessionTracker _tracker = null!;

    public SalonIntegrationTests(PostgresFixture fixture)
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
        _tracker = Substitute.For<IActiveSessionTracker>();
        _tracker.ActiveSalonId.Returns((Guid?)null);

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
    public async Task CreateSalon_ShouldPersistEventAndCreateProjection()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var configuration = CreateValidConfiguration();
        
        var command = new CreateSalonCommand(
            Id: salonId,
            Name: "Salon National France",
            IsDefault: true,
            IsTemporized: false,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 136.5m,
            TxCtcss: 136.5m,
            Configuration: configuration
        );

        // Act
        var commandResult = await CreateSalonCommandHandler.Handle(
            command,
            _repository,
            CancellationToken.None
        );

        // Sauvegarder les changements pour déclencher la projection
        await _session.SaveChangesAsync();

        // Assert - Valider que la commande a réussi
        commandResult.ShouldBeSuccess(id => id.Should().Be(salonId));

        // Valider que la projection a été créée via Query
        var query = new GetSalonByIdQuery(salonId);
        var queryResult = await GetSalonByIdQueryHandler.Handle(
            query,
            _repository,
            CancellationToken.None
        );

        queryResult.ShouldBeSuccess(salon =>
        {
            salon.Id.Should().Be(salonId);
            salon.Name.Should().Be("Salon National France");
            salon.IsDefault.Should().BeTrue();
            salon.IsTemporized.Should().BeFalse();
            salon.IsDeleted.Should().BeFalse();
            salon.Configuration.RxFrequency.Should().Be(145.550m);
            salon.Configuration.TxFrequency.Should().Be(145.550m);
            salon.Configuration.RxCtcss.Should().Be(136.5m);
            salon.Configuration.TxCtcss.Should().Be(136.5m);
            salon.Configuration.Host.Should().Be("ref.f5kri.fr");
            salon.Configuration.Port.Should().Be(5300);
        });
    }

    [Fact]
    public async Task CreateSalon_WithNullCtcss_ShouldPersistCorrectly()
    {
        // Arrange - Salon sans CTCSS
        var salonId = Guid.NewGuid();
        var configuration = CreateValidConfiguration();
        
        var command = new CreateSalonCommand(
            Id: salonId,
            Name: "Salon Sans CTCSS",
            IsDefault: false,
            IsTemporized: false,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: null, // Pas de CTCSS RX
            TxCtcss: null, // Pas de CTCSS TX
            Configuration: configuration
        );

        // Act
        var commandResult = await CreateSalonCommandHandler.Handle(
            command,
            _repository,
            CancellationToken.None
        );

        await _session.SaveChangesAsync();

        // Assert
        commandResult.ShouldBeSuccess();

        var query = new GetSalonByIdQuery(salonId);
        var queryResult = await GetSalonByIdQueryHandler.Handle(
            query,
            _repository,
            CancellationToken.None
        );

        queryResult.ShouldBeSuccess(salon =>
        {
            salon.Configuration.RxCtcss.Should().BeNull();
            salon.Configuration.TxCtcss.Should().BeNull();
        });
    }

    [Fact]
    public async Task CreateMultipleSalons_ShouldPersistAllCorrectly()
    {
        // Arrange
        var salon1Id = Guid.NewGuid();
        var salon2Id = Guid.NewGuid();
        var config = CreateValidConfiguration();

        var command1 = new CreateSalonCommand(
            salon1Id,
            "Salon 1",
            true,
            false,
            145.550m,
            145.550m,
            136.5m,
            136.5m,
            config
        );

        var command2 = new CreateSalonCommand(
            salon2Id,
            "Salon 2",
            false,
            true,
            145.575m,
            145.575m,
            123.0m,
            123.0m,
            config
        );

        // Act
        await CreateSalonCommandHandler.Handle(command1, _repository, CancellationToken.None);
        await CreateSalonCommandHandler.Handle(command2, _repository, CancellationToken.None);
        await _session.SaveChangesAsync();

        // Assert
        var result1 = await GetSalonByIdQueryHandler.Handle(
            new GetSalonByIdQuery(salon1Id),
            _repository,
            CancellationToken.None
        );

        var result2 = await GetSalonByIdQueryHandler.Handle(
            new GetSalonByIdQuery(salon2Id),
            _repository,
            CancellationToken.None
        );

        result1.ShouldBeSuccess(s => s.Name.Should().Be("Salon 1"));
        result2.ShouldBeSuccess(s => s.Name.Should().Be("Salon 2"));
    }

    [Fact]
    public async Task GetSalonById_WhenNotExists_ShouldReturnFailure()
    {
        // Arrange - ID qui n'existe pas
        var nonExistentId = Guid.NewGuid();

        // Act
        var query = new GetSalonByIdQuery(nonExistentId);
        var queryResult = await GetSalonByIdQueryHandler.Handle(
            query,
            _repository,
            CancellationToken.None
        );

        // Assert
        queryResult.ShouldBeFail(errors =>
        {
            errors.Should().ContainSingle();
            errors.Head.Code.Should().Be("SALON_NOT_FOUND");
        });
    }

    [Fact]
    public async Task GetActiveSalon_WhenNoSalonActive_ShouldReturnNull()
    {
        // Arrange - Créer un Salon mais ne pas l'activer
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var command = new CreateSalonCommand(
            salonId,
            "Salon Inactif",
            false,
            false,
            145.550m,
            145.550m,
            null,
            null,
            config
        );

        await CreateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);
        await _session.SaveChangesAsync();

        // Act
        var query = new GetActiveSalonQuery();
        var result = await GetActiveSalonQueryHandler.Handle(
            query,
            _repository,
            _tracker,
            CancellationToken.None
        );

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateSalon_WithInvalidFrequency_ShouldReturnFailure()
    {
        // Arrange - Fréquence invalide (hors plage 30-3000 MHz)
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var invalidCommand = new CreateSalonCommand(
            salonId,
            "Salon Invalide",
            false,
            false,
            RxFrequency: 5000m, // Invalide (> 3000 MHz)
            TxFrequency: 145.550m,
            RxCtcss: null,
            TxCtcss: null,
            Configuration: config
        );

        // Act
        var commandResult = await CreateSalonCommandHandler.Handle(
            invalidCommand,
            _repository,
            CancellationToken.None
        );

        // Assert
        commandResult.ShouldBeFail(errors =>
        {
            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Code.Contains("FREQUENCY"));
        });
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
