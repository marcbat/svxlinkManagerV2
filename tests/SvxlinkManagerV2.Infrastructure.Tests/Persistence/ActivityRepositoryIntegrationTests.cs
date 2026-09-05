using FluentAssertions;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Domain.Statistics;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests d'intégration d'<see cref="ActivityRepository"/> sur SQLite : ce sont les
/// regroupements délégués à la base qui sont vérifiés ici, pas de la logique en mémoire.
/// </summary>
[Trait("Category", "Integration")]
[Collection("PostgresIntegration")]
public class ActivityRepositoryIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SalonId = Guid.NewGuid();

    private readonly PostgresContainerFixture _fixture;
    private SvxlinkDbContext _context = null!;
    private ActivityRepository _repository = null!;

    public ActivityRepositoryIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repository = new ActivityRepository(_context);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context?.Dispose();
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Sessions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartSessionAsync_ShouldCloseThePreviousOne()
    {
        await _repository.StartSessionAsync(Session("TG208", Now.AddHours(-2)), CancellationToken.None);
        await _repository.StartSessionAsync(Session("TG209", Now.AddHours(-1)), CancellationToken.None);

        var sessions = await _repository.GetSessionsAsync(DateTimeOffset.MinValue, CancellationToken.None);

        sessions.Should().HaveCount(2);
        sessions.Count(s => s.IsOpen).Should().Be(1, "deux sessions ouvertes rendraient tous les cumuls de temps faux");

        var closed = sessions.Single(s => !s.IsOpen);
        closed.SalonName.Should().Be("TG208");
        closed.EndedAt.Should().Be(Now.AddHours(-1), "la session précédente se termine là où la suivante commence");
    }

    [Fact]
    public async Task CloseOpenSessionsAsync_ShouldFlagRecovery()
    {
        await _repository.StartSessionAsync(Session("TG208", Now.AddHours(-3)), CancellationToken.None);

        var result = await _repository.CloseOpenSessionsAsync(Now.AddHours(-2), true, CancellationToken.None);

        result.ShouldBeSuccess();
        var session = (await _repository.GetSessionsAsync(DateTimeOffset.MinValue, CancellationToken.None)).Single();
        session.ClosedOnRecovery.Should().BeTrue();
        session.Duration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task GetSessionsAsync_ShouldKeepSessionsOverlappingTheWindow()
    {
        // Close bien avant la fenêtre : hors sujet.
        var old = Session("Ancien", Now.AddDays(-10));
        old.Close(Now.AddDays(-9));
        _context.SalonSessions.Add(old);

        // Commencée avant la fenêtre mais close dedans : elle la recouvre et compte au prorata.
        var overlapping = Session("Chevauchant", Now.AddHours(-30));
        overlapping.Close(Now.AddHours(-20));
        _context.SalonSessions.Add(overlapping);
        await _context.SaveChangesAsync();

        // Toujours ouverte : elle recouvre la fenêtre quelle que soit son ancienneté.
        await _repository.StartSessionAsync(Session("Ouverte", Now.AddHours(-19)), CancellationToken.None);

        var sessions = await _repository.GetSessionsAsync(Now.AddHours(-24), CancellationToken.None);

        sessions.Select(s => s.SalonName).Should().BeEquivalentTo(["Chevauchant", "Ouverte"]);
    }

    // -------------------------------------------------------------------------
    // Agrégations d'événements
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetEventSummariesAsync_ShouldGroupCountsAndDurations()
    {
        await SeedTalkersAsync();
        await AddAsync(ActivityEvent.Create(ActivityEventType.DtmfCommand, Now.AddMinutes(-5), detail: "310"));

        var summaries = await _repository.GetEventSummariesAsync(Now.AddHours(-24), CancellationToken.None);

        var talker = summaries.Single(s => s.Type == ActivityEventType.TalkerHeard);
        talker.Count.Should().Be(3);
        talker.TotalSeconds.Should().Be(10 + 20 + 45);
        talker.MaxSeconds.Should().Be(45);

        var dtmf = summaries.Single(s => s.Type == ActivityEventType.DtmfCommand);
        dtmf.Count.Should().Be(1);
        dtmf.TotalSeconds.Should().Be(0, "une commande DTMF n'a pas de durée");
    }

    [Fact]
    public async Task GetEventSummariesAsync_ShouldIgnoreEventsBeforeTheWindow()
    {
        await AddAsync(ActivityEvent.Create(ActivityEventType.TalkerHeard, Now.AddDays(-3),
            callsign: "HB9AAA", duration: TimeSpan.FromSeconds(60)));
        await AddAsync(ActivityEvent.Create(ActivityEventType.TalkerHeard, Now.AddHours(-1),
            callsign: "HB9AAA", duration: TimeSpan.FromSeconds(10)));

        var summaries = await _repository.GetEventSummariesAsync(Now.AddHours(-24), CancellationToken.None);

        summaries.Single(s => s.Type == ActivityEventType.TalkerHeard).Count.Should().Be(1);
    }

    [Fact]
    public async Task GetTopCallsignsAsync_ShouldRankByCumulatedTime()
    {
        await SeedTalkersAsync();

        var top = await _repository.GetTopCallsignsAsync(Now.AddHours(-24), 10, CancellationToken.None);

        top.Should().HaveCount(2);
        top[0].Callsign.Should().Be("HB9BBB");
        top[0].Count.Should().Be(1);
        top[0].TotalSeconds.Should().Be(45);
        top[1].Callsign.Should().Be("HB9AAA");
        top[1].TotalSeconds.Should().Be(30);
        top[1].LastHeardAt.Should().Be(Now.AddHours(-1));
    }

    [Fact]
    public async Task GetTopCallsignsAsync_ShouldHonourTheLimit()
    {
        await SeedTalkersAsync();

        var top = await _repository.GetTopCallsignsAsync(Now.AddHours(-24), 1, CancellationToken.None);

        top.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetDistinctCallsignCountAsync_ShouldCountUniqueNodes()
    {
        await SeedTalkersAsync();

        var count = await _repository.GetDistinctCallsignCountAsync(Now.AddHours(-24), CancellationToken.None);

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetSalonEventSummariesAsync_ShouldSplitBySalon()
    {
        await AddAsync(ActivityEvent.Create(ActivityEventType.TalkerHeard, Now.AddHours(-2),
            SalonId, "TG208", "HB9AAA", TimeSpan.FromSeconds(10)));
        await AddAsync(ActivityEvent.Create(ActivityEventType.TalkerHeard, Now.AddHours(-1),
            Guid.NewGuid(), "TG209", "HB9BBB", TimeSpan.FromSeconds(30)));

        var summaries = await _repository.GetSalonEventSummariesAsync(
            Now.AddHours(-24), ActivityEventType.TalkerHeard, CancellationToken.None);

        summaries.Should().HaveCount(2);
        summaries.Single(s => s.SalonName == "TG209").TotalSeconds.Should().Be(30);
    }

    [Fact]
    public async Task GetDtmfSummariesAsync_ShouldCountEachCode()
    {
        await AddAsync(ActivityEvent.Create(ActivityEventType.DtmfCommand, Now.AddHours(-3), detail: "310"));
        await AddAsync(ActivityEvent.Create(ActivityEventType.DtmfCommand, Now.AddHours(-2), detail: "310"));
        await AddAsync(ActivityEvent.Create(ActivityEventType.DtmfCommand, Now.AddHours(-1), detail: "999"));

        var summaries = await _repository.GetDtmfSummariesAsync(Now.AddHours(-24), CancellationToken.None);

        summaries.Single(s => s.Code == "310").Count.Should().Be(2);
        summaries.Single(s => s.Code == "310").LastUsedAt.Should().Be(Now.AddHours(-2));
        summaries.Single(s => s.Code == "999").Count.Should().Be(1);
    }

    [Fact]
    public async Task GetHourlyActivityAsync_ShouldGroupOnTheFrozenLocalTime()
    {
        var moment = Now.AddHours(-2);
        await AddAsync(ActivityEvent.Create(ActivityEventType.TalkerHeard, moment,
            callsign: "HB9AAA", duration: TimeSpan.FromSeconds(10)));
        await AddAsync(ActivityEvent.Create(ActivityEventType.TalkerHeard, moment,
            callsign: "HB9BBB", duration: TimeSpan.FromSeconds(20)));

        var cells = await _repository.GetHourlyActivityAsync(Now.AddHours(-24), CancellationToken.None);

        var local = moment.ToLocalTime();
        var cell = cells.Single();
        cell.DayOfWeek.Should().Be((int)local.DayOfWeek);
        cell.Hour.Should().Be(local.Hour);
        cell.Count.Should().Be(2);
        cell.TotalSeconds.Should().Be(30);
    }

    [Fact]
    public async Task GetRecentEventsAsync_ShouldReturnTheNewestFirst()
    {
        await AddAsync(ActivityEvent.Create(ActivityEventType.DtmfCommand, Now.AddHours(-3), detail: "1"));
        await AddAsync(ActivityEvent.Create(ActivityEventType.DtmfCommand, Now.AddHours(-1), detail: "2"));
        await AddAsync(ActivityEvent.Create(ActivityEventType.DtmfCommand, Now.AddHours(-2), detail: "3"));

        var events = await _repository.GetRecentEventsAsync(Now.AddHours(-24), 2, CancellationToken.None);

        events.Select(e => e.Detail).Should().ContainInOrder("2", "3");
    }

    [Fact]
    public async Task HasAnyEventAsync_ShouldIgnoreTheWindow()
    {
        await AddAsync(ActivityEvent.Create(ActivityEventType.LocalTransmission, Now.AddDays(-400),
            duration: TimeSpan.FromSeconds(5)));

        (await _repository.HasAnyEventAsync(ActivityEventType.LocalTransmission, CancellationToken.None))
            .Should().BeTrue();
        (await _repository.HasAnyEventAsync(ActivityEventType.RxDistortion, CancellationToken.None))
            .Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Rétention
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PurgeBeforeAsync_ShouldDeleteOldRowsButKeepTheOpenSession()
    {
        await AddAsync(ActivityEvent.Create(ActivityEventType.DtmfCommand, Now.AddDays(-100), detail: "310"));
        await AddAsync(ActivityEvent.Create(ActivityEventType.DtmfCommand, Now.AddDays(-1), detail: "311"));

        var oldSession = Session("Ancien", Now.AddDays(-120));
        oldSession.Close(Now.AddDays(-119));
        _context.SalonSessions.Add(oldSession);
        await _context.SaveChangesAsync();

        // Ouverte depuis longtemps : elle décrit l'état courant, la purge ne doit pas l'emporter.
        await _repository.StartSessionAsync(Session("Courante", Now.AddDays(-200)), CancellationToken.None);

        var result = await _repository.PurgeBeforeAsync(Now.AddDays(-90), CancellationToken.None);

        result.ShouldBeSuccess(deleted => deleted.Should().Be(2));

        var remaining = await _repository.GetSessionsAsync(DateTimeOffset.MinValue, CancellationToken.None);
        remaining.Select(s => s.SalonName).Should().BeEquivalentTo(["Courante"]);

        var events = await _repository.GetRecentEventsAsync(DateTimeOffset.MinValue, 100, CancellationToken.None);
        events.Select(e => e.Detail).Should().BeEquivalentTo(["311"]);
    }

    [Fact]
    public async Task ResetAsync_ShouldEmptyBothTables()
    {
        await SeedTalkersAsync();
        await _repository.StartSessionAsync(Session("TG208", Now.AddHours(-1)), CancellationToken.None);

        var result = await _repository.ResetAsync(CancellationToken.None);

        result.ShouldBeSuccess();
        (await _repository.GetSessionsAsync(DateTimeOffset.MinValue, CancellationToken.None)).Should().BeEmpty();
        (await _repository.GetFirstActivityAtAsync(CancellationToken.None)).Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Bornes de l'historique
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetFirstActivityAtAsync_ShouldReturnNullOnAnEmptyHistory()
    {
        (await _repository.GetFirstActivityAtAsync(CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task GetFirstActivityAtAsync_ShouldConsiderSessionsAndEvents()
    {
        await AddAsync(ActivityEvent.Create(ActivityEventType.DtmfCommand, Now.AddHours(-5), detail: "310"));
        await _repository.StartSessionAsync(Session("TG208", Now.AddHours(-12)), CancellationToken.None);

        (await _repository.GetFirstActivityAtAsync(CancellationToken.None)).Should().Be(Now.AddHours(-12));
    }

    [Fact]
    public async Task GetLastActivityAtAsync_ShouldFallBackOnClosedSessionsWhenNoEvent()
    {
        await _repository.StartSessionAsync(Session("TG208", Now.AddHours(-5)), CancellationToken.None);
        await _repository.CloseOpenSessionsAsync(Now.AddHours(-4), false, CancellationToken.None);

        (await _repository.GetLastActivityAtAsync(CancellationToken.None)).Should().Be(Now.AddHours(-4));
    }

    // -------------------------------------------------------------------------
    // Utilitaires
    // -------------------------------------------------------------------------

    private static SalonSession Session(string name, DateTimeOffset startedAt)
        => SalonSession.Start(SalonId, name, SalonKind.Reflector, SalonActivationOrigin.Web, startedAt);

    private async Task AddAsync(ActivityEvent activityEvent)
        => (await _repository.AddEventAsync(activityEvent, CancellationToken.None)).ShouldBeSuccess();

    private async Task SeedTalkersAsync()
    {
        await AddAsync(ActivityEvent.Create(ActivityEventType.TalkerHeard, Now.AddHours(-3),
            SalonId, "TG208", "HB9AAA", TimeSpan.FromSeconds(10)));
        await AddAsync(ActivityEvent.Create(ActivityEventType.TalkerHeard, Now.AddHours(-1),
            SalonId, "TG208", "HB9AAA", TimeSpan.FromSeconds(20)));
        await AddAsync(ActivityEvent.Create(ActivityEventType.TalkerHeard, Now.AddHours(-2),
            SalonId, "TG208", "HB9BBB", TimeSpan.FromSeconds(45)));
    }
}
