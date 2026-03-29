using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Features.SA818.GetSA818Configuration;
using SvxlinkManagerV2.Application.Features.SA818.UpdateSA818Configuration;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using static LanguageExt.Prelude;
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

    // ── helpers ────────────────────────────────────────────────────────────────

    private GetSA818ConfigurationQueryHandler CreateQueryHandler(
        ISalonRepository? salonRepository = null,
        IActiveSessionTracker? tracker = null)
    {
        var salonRepo = salonRepository ?? Substitute.For<ISalonRepository>();
        var sessionTracker = tracker ?? Substitute.For<IActiveSessionTracker>();
        return new GetSA818ConfigurationQueryHandler(_repository, salonRepo, sessionTracker);
    }

    private static SvxLinkConfiguration CreateValidSalonConfiguration(
        decimal rxFrequency = 145.550m,
        decimal txFrequency = 145.550m,
        decimal? rxCtcss = null,
        decimal? txCtcss = null)
    {
        return new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d", 16000, 1,
            "ref.f5kri.fr", 5300,
            "F5ABC-L", "test-auth-key-123", "OPUS", 0,
            "F5ABC", "ModuleHelp,ModuleParrot", 60, 60,
            "71.9", "/usr/share/svxlink/events.tcl", "fr_FR", 0,
            Guid.NewGuid(),
            rxFrequency, txFrequency, rxCtcss, txCtcss);
    }

    // ── tests existants ────────────────────────────────────────────────────────

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

        var queryHandler = CreateQueryHandler();
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

        var queryHandler = CreateQueryHandler();
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
        var queryHandler = CreateQueryHandler();
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

    // ── nouveaux tests : RX/TX/CTCSS depuis salon actif ───────────────────────

    [Fact]
    public async Task GetSA818Configuration_WhenActiveSalonExists_ShouldReturnFrequencyAndCtcss()
    {
        // Arrange
        var sa818 = SA818Aggregate.Create(4, 4, SA818Bandwidth.Wide25kHz, false, false, false)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        await _repository.SaveAsync(sa818, CancellationToken.None);

        var salonId = Guid.NewGuid();
        var salonConfig = CreateValidSalonConfiguration(
            rxFrequency: 145.550m,
            txFrequency: 145.775m,
            rxCtcss: 88.5m,
            txCtcss: 88.5m);
        var salon = SalonAggregate.Create(salonId, "Salon Test RX/TX", false, false, salonConfig)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        var salonRepository = new SalonRepository(_context);
        await salonRepository.SaveAsync(salon, CancellationToken.None);

        var tracker = Substitute.For<IActiveSessionTracker>();
        tracker.ActiveSalonId.Returns((Guid?)salonId);

        var queryHandler = CreateQueryHandler(salonRepository, tracker);

        // Act
        var queryResult = await queryHandler.Handle(new GetSA818ConfigurationQuery(), CancellationToken.None);

        // Assert
        queryResult.ShouldBeSuccess(config =>
        {
            config.RxFrequency.Should().Be(145.550m);
            config.TxFrequency.Should().Be(145.775m);
            config.RxCtcss.Should().Be(88.5m);
            config.TxCtcss.Should().Be(88.5m);
        });
    }

    [Fact]
    public async Task GetSA818Configuration_WhenActiveSalonExistsWithoutCtcss_ShouldReturnNullCtcss()
    {
        // Arrange
        var sa818 = SA818Aggregate.Create(4, 4, SA818Bandwidth.Wide25kHz, false, false, false)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        await _repository.SaveAsync(sa818, CancellationToken.None);

        var salonId = Guid.NewGuid();
        var salonConfig = CreateValidSalonConfiguration(
            rxFrequency: 145.550m,
            txFrequency: 145.550m,
            rxCtcss: null,
            txCtcss: null);
        var salon = SalonAggregate.Create(salonId, "Salon Sans CTCSS", false, false, salonConfig)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        var salonRepository = new SalonRepository(_context);
        await salonRepository.SaveAsync(salon, CancellationToken.None);

        var tracker = Substitute.For<IActiveSessionTracker>();
        tracker.ActiveSalonId.Returns((Guid?)salonId);

        var queryHandler = CreateQueryHandler(salonRepository, tracker);

        // Act
        var queryResult = await queryHandler.Handle(new GetSA818ConfigurationQuery(), CancellationToken.None);

        // Assert
        queryResult.ShouldBeSuccess(config =>
        {
            config.RxFrequency.Should().Be(145.550m);
            config.TxFrequency.Should().Be(145.550m);
            config.RxCtcss.Should().BeNull();
            config.TxCtcss.Should().BeNull();
        });
    }

    [Fact]
    public async Task GetSA818Configuration_WhenNoActiveSalon_ShouldReturnNullFrequencyFields()
    {
        // Arrange
        var sa818 = SA818Aggregate.Create(4, 4, SA818Bandwidth.Wide25kHz, false, false, false)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());
        await _repository.SaveAsync(sa818, CancellationToken.None);

        var tracker = Substitute.For<IActiveSessionTracker>();
        tracker.ActiveSalonId.Returns((Guid?)null);

        var queryHandler = CreateQueryHandler(tracker: tracker);

        // Act
        var queryResult = await queryHandler.Handle(new GetSA818ConfigurationQuery(), CancellationToken.None);

        // Assert
        queryResult.ShouldBeSuccess(config =>
        {
            config.RxFrequency.Should().BeNull();
            config.TxFrequency.Should().BeNull();
            config.RxCtcss.Should().BeNull();
            config.TxCtcss.Should().BeNull();
        });
    }
}
