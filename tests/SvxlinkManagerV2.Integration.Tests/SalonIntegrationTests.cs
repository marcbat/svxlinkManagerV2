using FluentAssertions;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.CreateSalon;
using SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;
using SvxlinkManagerV2.Application.Features.Salons.GetSalonById;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// Tests d'intégration validant le workflow complet Salon :
/// Command → Persistance EF Core → Query
/// </summary>
[Trait("Category", "Integration")]
[Collection("IntegrationTests")]
public class SalonIntegrationTests : IAsyncLifetime
{
    private readonly SqliteFixture _fixture;
    private SvxlinkDbContext _context = null!;
    private SalonRepository _repository = null!;
    private IActiveSessionTracker _tracker = null!;

    public SalonIntegrationTests(SqliteFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repository = new SalonRepository(_context);
        _tracker = Substitute.For<IActiveSessionTracker>();
        _tracker.ActiveSalonId.Returns((Guid?)null);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateSalon_ShouldPersistAndRetrieveCorrectly()
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
        var handler = new CreateSalonCommandHandler(_repository);
        var commandResult = await handler.Handle(command, CancellationToken.None);

        // Assert
        commandResult.ShouldBeSuccess(id => id.Should().Be(salonId));

        var queryHandler = new GetSalonByIdQueryHandler(_repository);
        var queryResult = await queryHandler.Handle(new GetSalonByIdQuery(salonId), CancellationToken.None);

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
        // Arrange
        var salonId = Guid.NewGuid();
        var configuration = CreateValidConfiguration();

        var command = new CreateSalonCommand(
            Id: salonId,
            Name: "Salon Sans CTCSS",
            IsDefault: false,
            IsTemporized: false,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: null,
            TxCtcss: null,
            Configuration: configuration
        );

        // Act
        var handler = new CreateSalonCommandHandler(_repository);
        var commandResult = await handler.Handle(command, CancellationToken.None);

        // Assert
        commandResult.ShouldBeSuccess();

        var queryHandler = new GetSalonByIdQueryHandler(_repository);
        var queryResult = await queryHandler.Handle(new GetSalonByIdQuery(salonId), CancellationToken.None);

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

        var handler = new CreateSalonCommandHandler(_repository);

        var command1 = new CreateSalonCommand(salon1Id, "Salon 1", true, false, 145.550m, 145.550m, 136.5m, 136.5m, config);
        var command2 = new CreateSalonCommand(salon2Id, "Salon 2", false, true, 145.575m, 145.575m, 123.0m, 123.0m, config);

        // Act
        await handler.Handle(command1, CancellationToken.None);
        await handler.Handle(command2, CancellationToken.None);

        // Assert
        var queryHandler = new GetSalonByIdQueryHandler(_repository);
        var result1 = await queryHandler.Handle(new GetSalonByIdQuery(salon1Id), CancellationToken.None);
        var result2 = await queryHandler.Handle(new GetSalonByIdQuery(salon2Id), CancellationToken.None);

        result1.ShouldBeSuccess(s => s.Name.Should().Be("Salon 1"));
        result2.ShouldBeSuccess(s => s.Name.Should().Be("Salon 2"));
    }

    [Fact]
    public async Task GetSalonById_WhenNotExists_ShouldReturnFailure()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var queryHandler = new GetSalonByIdQueryHandler(_repository);
        var queryResult = await queryHandler.Handle(new GetSalonByIdQuery(nonExistentId), CancellationToken.None);

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
        // Arrange
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var command = new CreateSalonCommand(salonId, "Salon Inactif", false, false, 145.550m, 145.550m, null, null, config);

        var createHandler = new CreateSalonCommandHandler(_repository);
        await createHandler.Handle(command, CancellationToken.None);

        // Act
        var queryHandler = new GetActiveSalonQueryHandler(_repository, _tracker);
        var result = await queryHandler.Handle(new GetActiveSalonQuery(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateSalon_WithInvalidFrequency_ShouldReturnFailure()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var invalidCommand = new CreateSalonCommand(
            salonId, "Salon Invalide", false, false,
            RxFrequency: 5000m, // Invalide (> 3000 MHz)
            TxFrequency: 145.550m,
            RxCtcss: null, TxCtcss: null,
            Configuration: config);

        // Act
        var handler = new CreateSalonCommandHandler(_repository);
        var commandResult = await handler.Handle(invalidCommand, CancellationToken.None);

        // Assert
        commandResult.ShouldBeFail(errors =>
        {
            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Code.Contains("FREQUENCY"));
        });
    }

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
            Guid.NewGuid(),
            145.550m,
            145.550m,
            136.5m,
            136.5m);
    }
}
