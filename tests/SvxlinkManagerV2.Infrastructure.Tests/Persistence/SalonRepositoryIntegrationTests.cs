using FluentAssertions;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests d'intégration pour SalonRepository avec EF Core + SQLite.
/// </summary>
[Trait("Category", "Integration")]
[Collection("PostgresIntegration")]
public class SalonRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private SvxlinkDbContext _context = null!;
    private SalonRepository _repository = null!;

    public SalonRepositoryIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repository = new SalonRepository(_context);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistSalon()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(salonId, "Salon National France", true, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        // Act
        var saveResult = await _repository.SaveAsync(salon, CancellationToken.None);

        // Assert
        saveResult.ShouldBeSuccess();

        var reloadResult = await _repository.GetByIdAsync(salonId, CancellationToken.None);
        reloadResult.ShouldBeSuccess(reloaded =>
        {
            reloaded.Id.Should().Be(salonId);
            reloaded.Name.Should().Be("Salon National France");
            reloaded.IsDefault.Should().BeTrue();
            reloaded.IsTemporized.Should().BeFalse();
            reloaded.Configuration.Host.Should().Be(config.Host);
            reloaded.Configuration.Port.Should().Be(config.Port);
            reloaded.Configuration.Callsign.Should().Be(config.Callsign);
            reloaded.Configuration.AuthKey.Should().Be(config.AuthKey);
        });
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
    public async Task UpdateConfiguration_ShouldPersistUpdatedConfiguration()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(salonId, "Salon Config Update Test", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        await _repository.SaveAsync(salon, CancellationToken.None);

        // Act
        var updatedConfig = config with { Host = "ref.newhost.fr", Port = 6300 };
        salon.UpdateConfiguration(updatedConfig);
        await _repository.SaveAsync(salon, CancellationToken.None);

        // Assert
        var reloadResult = await _repository.GetByIdAsync(salonId, CancellationToken.None);
        reloadResult.ShouldBeSuccess(reloaded =>
        {
            reloaded.Configuration.Host.Should().Be("ref.newhost.fr");
            reloaded.Configuration.Port.Should().Be(6300);
        });
    }

    [Fact]
    public async Task Delete_ShouldSoftDeleteSalon()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(salonId, "Salon Delete Test", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        await _repository.SaveAsync(salon, CancellationToken.None);

        // Act
        var deleteResult = await _repository.DeleteAsync(salonId, CancellationToken.None);

        // Assert
        deleteResult.ShouldBeSuccess();
        var reloadResult = await _repository.GetByIdAsync(salonId, CancellationToken.None);
        reloadResult.ShouldBeFail();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnOnlyNonDeletedSalons()
    {
        // Arrange - each salon needs its own config instance to avoid EF Core tracking conflicts
        var salon1 = SalonAggregate.Create(Guid.NewGuid(), "Salon 1", false, false, CreateValidConfiguration())
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        var salon2 = SalonAggregate.Create(Guid.NewGuid(), "Salon 2", false, false, CreateValidConfiguration())
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        var salon3 = SalonAggregate.Create(Guid.NewGuid(), "Salon 3 (Deleted)", false, false, CreateValidConfiguration())
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        (await _repository.SaveAsync(salon1, CancellationToken.None)).ShouldBeSuccess();
        (await _repository.SaveAsync(salon2, CancellationToken.None)).ShouldBeSuccess();
        (await _repository.SaveAsync(salon3, CancellationToken.None)).ShouldBeSuccess();
        (await _repository.DeleteAsync(salon3.Id, CancellationToken.None)).ShouldBeSuccess();

        // Act
        var result = await _repository.GetAllAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Name == "Salon 1");
        result.Should().Contain(s => s.Name == "Salon 2");
        result.Should().NotContain(s => s.Name == "Salon 3 (Deleted)");
    }

    private static SvxLinkConfiguration CreateValidConfiguration()
    {
        return new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d", 16000, 1,
            "ref.f5kri.fr", 5300,
            "F5ABC-L", "test-auth-key-123", 0,
            "F5ABC", "ModuleHelp,ModuleParrot", 60, 60,
            "71.9", "/usr/share/svxlink/events.tcl", "fr_FR", 0,
            145.550m, 145.550m, 136.5m, 136.5m);
    }
}
