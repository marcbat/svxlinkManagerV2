using FluentAssertions;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Features.SA818.GetSA818Configuration;
using SvxlinkManagerV2.Application.Features.SA818.UpdateSA818Configuration;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// Tests d'intégration validant le workflow complet SA818 :
/// Command → Persistance EF Core → Query
/// </summary>
[Trait("Category", "Integration")]
[Collection("IntegrationTests")]
public class SA818IntegrationTests : IAsyncLifetime
{
    private readonly SqliteFixture _fixture;
    private SvxlinkDbContext _context = null!;
    private SA818Repository _repository = null!;

    public SA818IntegrationTests(SqliteFixture fixture)
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
    public async Task UpdateSA818Configuration_ShouldPersistAndRetrieveCorrectly()
    {
        // Arrange
        var command = new UpdateSA818ConfigurationCommand(
            Volume: 5,
            Squelch: 3,
            Bandwidth: SA818Bandwidth.Narrow12_5kHz,
            PreEmph: true,
            HighPass: true,
            LowPass: false
        );

        // Act
        var handler = new UpdateSA818ConfigurationCommandHandler(_repository);
        var commandResult = await handler.Handle(command, CancellationToken.None);

        // Assert
        commandResult.ShouldBeSuccess();

        var queryHandler = new GetSA818ConfigurationQueryHandler(_repository);
        var queryResult = await queryHandler.Handle(new GetSA818ConfigurationQuery(), CancellationToken.None);

        queryResult.ShouldBeSuccess(config =>
        {
            config.Id.Should().Be(SA818Aggregate.FixedId);
            config.Volume.Should().Be(5);
            config.Squelch.Should().Be(3);
            config.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
            config.PreEmph.Should().BeTrue();
            config.HighPass.Should().BeTrue();
            config.LowPass.Should().BeFalse();
            config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        });
    }

    [Fact]
    public async Task UpdateSA818Configuration_MultipleTimes_ShouldKeepLatestConfiguration()
    {
        // Arrange
        var firstCommand = new UpdateSA818ConfigurationCommand(4, 2, SA818Bandwidth.Wide25kHz, false, false, true);
        var handler = new UpdateSA818ConfigurationCommandHandler(_repository);

        await handler.Handle(firstCommand, CancellationToken.None);

        // Act
        var secondCommand = new UpdateSA818ConfigurationCommand(7, 5, SA818Bandwidth.Narrow12_5kHz, true, true, true);
        var updateResult = await handler.Handle(secondCommand, CancellationToken.None);

        // Assert
        updateResult.ShouldBeSuccess();

        var queryHandler = new GetSA818ConfigurationQueryHandler(_repository);
        var queryResult = await queryHandler.Handle(new GetSA818ConfigurationQuery(), CancellationToken.None);

        queryResult.ShouldBeSuccess(config =>
        {
            config.Volume.Should().Be(7);
            config.Squelch.Should().Be(5);
            config.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
            config.PreEmph.Should().BeTrue();
            config.HighPass.Should().BeTrue();
            config.LowPass.Should().BeTrue();
        });
    }

    [Fact]
    public async Task GetSA818Configuration_WhenNotInitialized_ShouldReturnFailure()
    {
        // Act
        var queryHandler = new GetSA818ConfigurationQueryHandler(_repository);
        var queryResult = await queryHandler.Handle(new GetSA818ConfigurationQuery(), CancellationToken.None);

        // Assert
        queryResult.ShouldBeFail(errors =>
        {
            errors.Should().ContainSingle();
            errors.Head.Code.Should().Be("SA818_NOT_FOUND");
        });
    }

    [Fact]
    public async Task UpdateSA818Configuration_WithInvalidVolume_ShouldReturnFailure()
    {
        // Arrange
        var invalidCommand = new UpdateSA818ConfigurationCommand(
            Volume: 10, // Invalide (> 8)
            Squelch: 3,
            Bandwidth: SA818Bandwidth.Narrow12_5kHz,
            PreEmph: true,
            HighPass: true,
            LowPass: false
        );

        // Act
        var handler = new UpdateSA818ConfigurationCommandHandler(_repository);
        var commandResult = await handler.Handle(invalidCommand, CancellationToken.None);

        // Assert
        commandResult.ShouldBeFail(errors =>
        {
            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Code.Contains("VOLUME"));
        });
    }
}
