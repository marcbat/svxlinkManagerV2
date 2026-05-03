using FluentAssertions;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests d'intégration pour SA818Repository avec EF Core + SQLite.
/// </summary>
[Trait("Category", "Integration")]
[Collection("PostgresIntegration")]
public class SA818RepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private SvxlinkDbContext _context = null!;
    private SA818Repository _repository = null!;

    public SA818RepositoryIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repository = new SA818Repository(_context);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistSA818()
    {
        // Arrange
        var sa818 = SA818Aggregate.Create(4, 4, SA818Bandwidth.Wide25kHz, false, false, false)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        // Act
        var saveResult = await _repository.SaveAsync(sa818, CancellationToken.None);

        // Assert
        saveResult.ShouldBeSuccess();

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
    public async Task GetAsync_ShouldPersistAndReloadSA818()
    {
        // Arrange
        var sa818 = SA818Aggregate.Create(6, 3, SA818Bandwidth.Narrow12_5kHz, true, true, false)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        await _repository.SaveAsync(sa818, CancellationToken.None);

        // Act
        var result = await _repository.GetAsync(CancellationToken.None);

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
    }

    [Fact]
    public async Task GetConfigurationAsync_WhenSA818NotFound_ShouldReturnNull()
    {
        // Act
        var configuration = await _repository.GetConfigurationAsync(CancellationToken.None);

        // Assert
        configuration.Should().BeNull();
    }

    [Fact]
    public async Task UpdateConfiguration_ShouldPersistUpdatedValues()
    {
        // Arrange
        var sa818 = SA818Aggregate.Create(4, 4, SA818Bandwidth.Wide25kHz, false, false, false)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        await _repository.SaveAsync(sa818, CancellationToken.None);

        var reloadResult = await _repository.GetAsync(CancellationToken.None);
        var reloaded = reloadResult.Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        // Act
        reloaded.UpdateConfiguration(8, 2, SA818Bandwidth.Narrow12_5kHz, true, true, true).ShouldBeSuccess();
        await _repository.SaveAsync(reloaded, CancellationToken.None);

        // Assert
        var finalResult = await _repository.GetAsync(CancellationToken.None);
        finalResult.ShouldBeSuccess(final =>
        {
            final.Volume.Should().Be(8);
            final.Squelch.Should().Be(2);
            final.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
            final.PreEmph.Should().BeTrue();
            final.HighPass.Should().BeTrue();
            final.LowPass.Should().BeTrue();
        });
    }
}
