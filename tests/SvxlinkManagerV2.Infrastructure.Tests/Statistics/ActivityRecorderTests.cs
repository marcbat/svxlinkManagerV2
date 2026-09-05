using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Statistics;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using SvxlinkManagerV2.Infrastructure.Statistics;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Statistics;

/// <summary>
/// Tests de l'enregistreur d'activité, en particulier de la tenue des intervalles ouverts :
/// une période de liaison n'est écrite qu'à sa fin, une interruption qu'à son rétablissement.
/// </summary>
[Trait("Category", "Integration")]
public class ActivityRecorderTests : IAsyncLifetime, IDisposable
{
    private readonly string _databasePath = $"activity-recorder-{Guid.NewGuid():N}.db";
    private ServiceProvider _provider = null!;
    private ActivityRecorder _recorder = null!;

    public Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SvxlinkDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        services.AddScoped<IActivityRepository, ActivityRepository>();

        _provider = services.BuildServiceProvider();

        using (var scope = _provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SvxlinkDbContext>().Database.EnsureCreated();
        }

        _recorder = new ActivityRecorder(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ILogger<ActivityRecorder>>());

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _provider?.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => _provider?.Dispose();

    // -------------------------------------------------------------------------
    // Sessions et attribution des événements
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RecordSessionStartAsync_ShouldOpenASingleSession()
    {
        var salonId = Guid.NewGuid();

        await _recorder.RecordSessionStartAsync(salonId, "TG208", SalonKind.Reflector, SalonActivationOrigin.Dtmf);

        var session = (await ReadSessionsAsync()).Single();
        session.SalonId.Should().Be(salonId);
        session.SalonName.Should().Be("TG208");
        session.Kind.Should().Be(SalonKind.Reflector);
        session.Origin.Should().Be(SalonActivationOrigin.Dtmf);
        session.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task RecordEventAsync_ShouldAttributeTheEventToTheCurrentSalon()
    {
        var salonId = Guid.NewGuid();
        await _recorder.RecordSessionStartAsync(salonId, "TG208", SalonKind.Reflector, SalonActivationOrigin.Web);

        await _recorder.RecordEventAsync(ActivityEventType.TalkerHeard,
            callsign: "HB9AAA", duration: TimeSpan.FromSeconds(12));

        var recorded = (await ReadEventsAsync()).Single(e => e.Type == ActivityEventType.TalkerHeard);
        recorded.SalonId.Should().Be(salonId);
        recorded.SalonName.Should().Be("TG208");
        recorded.Callsign.Should().Be("HB9AAA");
        recorded.DurationSeconds.Should().Be(12);
    }

    [Fact]
    public async Task RecordSessionStartAsync_ShouldNameStandalonePeriodsConsistently()
    {
        await _recorder.RecordSessionStartAsync(null, ActivityLabels.StandaloneSalonName,
            SalonKind.Standalone, SalonActivationOrigin.Startup);

        await _recorder.RecordEventAsync(ActivityEventType.DtmfCommand, detail: "310");

        var recorded = (await ReadEventsAsync()).Single();
        recorded.SalonId.Should().BeNull();
        recorded.SalonName.Should().Be(ActivityLabels.StandaloneSalonName);
    }

    [Fact]
    public async Task CloseCurrentSessionAsync_ShouldDetachTheSalonContext()
    {
        await _recorder.RecordSessionStartAsync(Guid.NewGuid(), "TG208", SalonKind.Reflector, SalonActivationOrigin.Web);

        await _recorder.CloseCurrentSessionAsync();
        await _recorder.RecordEventAsync(ActivityEventType.DtmfCommand, detail: "311");

        (await ReadSessionsAsync()).Single().IsOpen.Should().BeFalse();
        (await ReadEventsAsync()).Single().SalonId.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Intervalles de liaison
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RecordLinkStateAsync_ShouldNotWriteAnythingWhileTheLinkHolds()
    {
        await Link(ReflectorLinkStatus.Connecting);
        await Link(ReflectorLinkStatus.Connected);

        // Rien n'est écrit tant que la période court : sa durée n'est pas encore connue.
        (await ReadEventsAsync()).Should().BeEmpty();
        _recorder.PendingLinkUpSince.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordLinkStateAsync_ShouldWriteTheLinkedPeriodOnLoss()
    {
        await Link(ReflectorLinkStatus.Connected);
        await Link(ReflectorLinkStatus.Disconnected, ReflectorLinkFailureReason.HeartbeatTimeout);

        var events = await ReadEventsAsync();
        events.Should().Contain(e => e.Type == ActivityEventType.ReflectorLinkUp);
        events.Single(e => e.Type == ActivityEventType.ReflectorLinkLost)
            .Detail.Should().Be("plus de battement de cœur");
        _recorder.PendingLinkUpSince.Should().BeNull();
    }

    [Fact]
    public async Task RecordLinkStateAsync_ShouldWriteTheOutageOnRecovery()
    {
        await Link(ReflectorLinkStatus.Connected);
        await Link(ReflectorLinkStatus.Disconnected, ReflectorLinkFailureReason.RemoteDisconnected);
        await Link(ReflectorLinkStatus.Connecting);
        await Link(ReflectorLinkStatus.Connected);

        var events = await ReadEventsAsync();
        events.Should().ContainSingle(e => e.Type == ActivityEventType.ReflectorOutage);
        _recorder.PendingLinkUpSince.Should().NotBeNull("une nouvelle période de liaison a commencé");
    }

    [Fact]
    public async Task RecordLinkStateAsync_ShouldRecordAFailureWithoutLinkedPeriod()
    {
        await Link(ReflectorLinkStatus.Connecting);
        await Link(ReflectorLinkStatus.Failed, ReflectorLinkFailureReason.AuthenticationRejected);

        var events = await ReadEventsAsync();
        events.Should().ContainSingle(e => e.Type == ActivityEventType.ReflectorLinkFailed);
        events.Should().NotContain(e => e.Type == ActivityEventType.ReflectorLinkUp,
            "aucune liaison n'avait été établie");
        events.Single().Detail.Should().Be("authentification refusée");
    }

    [Fact]
    public async Task RecordLinkStateAsync_ShouldCloseTheLinkedPeriodOnSalonSwitchWithoutRecordingALoss()
    {
        await Link(ReflectorLinkStatus.Connected);

        // Changement de salon : le tracker repasse en Connecting, ce n'est pas une panne.
        await Link(ReflectorLinkStatus.Connecting);

        var events = await ReadEventsAsync();
        events.Should().ContainSingle(e => e.Type == ActivityEventType.ReflectorLinkUp);
        events.Should().NotContain(e => e.Type == ActivityEventType.ReflectorLinkLost);
    }

    [Fact]
    public async Task RecordLinkStateAsync_ShouldForgetTheOutageWhenNoLinkIsExpected()
    {
        await Link(ReflectorLinkStatus.Connected);
        await Link(ReflectorLinkStatus.Disconnected, ReflectorLinkFailureReason.RemoteDisconnected);

        // Bascule sur un salon perroquet : plus aucune liaison n'est attendue, l'interruption
        // ne doit pas courir jusqu'au prochain salon réflecteur.
        await Link(ReflectorLinkStatus.NotApplicable);
        await Link(ReflectorLinkStatus.Connecting);
        await Link(ReflectorLinkStatus.Connected);

        (await ReadEventsAsync()).Should().NotContain(e => e.Type == ActivityEventType.ReflectorOutage);
    }

    [Fact]
    public async Task RecordLinkStateAsync_ShouldIgnoreARepeatedState()
    {
        await Link(ReflectorLinkStatus.Connected);
        var since = _recorder.PendingLinkUpSince;

        await Link(ReflectorLinkStatus.Connected);

        _recorder.PendingLinkUpSince.Should().Be(since);
        (await ReadEventsAsync()).Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Utilitaires
    // -------------------------------------------------------------------------

    private Task Link(ReflectorLinkStatus status, ReflectorLinkFailureReason reason = ReflectorLinkFailureReason.None)
        => _recorder.RecordLinkStateAsync(new ReflectorLinkState(status, reason));

    private async Task<IReadOnlyList<SalonSession>> ReadSessionsAsync()
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SvxlinkDbContext>()
            .SalonSessions.ToListAsync();
    }

    private async Task<IReadOnlyList<ActivityEvent>> ReadEventsAsync()
    {
        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SvxlinkDbContext>()
            .ActivityEvents.ToListAsync();
    }
}
