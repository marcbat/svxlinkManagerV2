using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Statistics;
using SvxlinkManagerV2.Application.Features.Statistics.GetStatistics;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Application.Tests.Features.Statistics;

/// <summary>
/// Tests de l'agrégation de l'historique d'activité.
/// </summary>
public class GetStatisticsQueryHandlerTests
{
    private readonly IActivityRepository _repository = Substitute.For<IActivityRepository>();
    private readonly ISalonRepository _salonRepository = Substitute.For<ISalonRepository>();
    private readonly IActivityRecorder _recorder = Substitute.For<IActivityRecorder>();

    public GetStatisticsQueryHandlerTests()
    {
        _repository.GetSessionsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.GetEventSummariesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.GetSalonEventSummariesAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<ActivityEventType>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.GetTopCallsignsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.GetDtmfSummariesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.GetHourlyActivityAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.GetRecentEventsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _salonRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    // -------------------------------------------------------------------------
    // Cumul du temps par salon
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildSalonUsage_ShouldClampASessionStartedBeforeTheWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var from = now.AddHours(-24);

        var session = SalonSession.Start(Guid.NewGuid(), "TG208", SalonKind.Reflector, SalonActivationOrigin.Web,
            now.AddHours(-30));
        session.Close(now.AddHours(-20));

        var usage = GetStatisticsQueryHandler.BuildSalonUsage([session], from, now);

        usage.Single().TotalTime.Should().BeCloseTo(TimeSpan.FromHours(4), TimeSpan.FromSeconds(1),
            "seules les 4 heures situées dans la fenêtre comptent");
    }

    [Fact]
    public void BuildSalonUsage_ShouldNotCountAnActivationInheritedFromBeforeTheWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var from = now.AddHours(-24);

        var session = SalonSession.Start(Guid.NewGuid(), "TG208", SalonKind.Reflector, SalonActivationOrigin.Web,
            now.AddHours(-30));
        session.Close(now.AddHours(-20));

        var usage = GetStatisticsQueryHandler.BuildSalonUsage([session], from, now);

        usage.Single().SessionCount.Should().Be(0, "la session a commencé avant la fenêtre observée");
    }

    [Fact]
    public void BuildSalonUsage_ShouldRunAnOpenSessionUpToNow()
    {
        var now = DateTimeOffset.UtcNow;
        var session = SalonSession.Start(Guid.NewGuid(), "TG208", SalonKind.Reflector, SalonActivationOrigin.Web,
            now.AddHours(-2));

        var usage = GetStatisticsQueryHandler.BuildSalonUsage([session], now.AddHours(-24), now);

        usage.Single().IsOngoing.Should().BeTrue();
        usage.Single().TotalTime.Should().BeCloseTo(TimeSpan.FromHours(2), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void BuildSalonUsage_ShouldGroupSessionsOfTheSameSalonEvenAfterARename()
    {
        var now = DateTimeOffset.UtcNow;
        var salonId = Guid.NewGuid();

        var first = SalonSession.Start(salonId, "Ancien nom", SalonKind.Reflector, SalonActivationOrigin.Web,
            now.AddHours(-6));
        first.Close(now.AddHours(-5));

        var second = SalonSession.Start(salonId, "Nouveau nom", SalonKind.Reflector, SalonActivationOrigin.Dtmf,
            now.AddHours(-3));
        second.Close(now.AddHours(-2));

        var usage = GetStatisticsQueryHandler.BuildSalonUsage([first, second], now.AddHours(-24), now);

        usage.Should().HaveCount(1);
        usage[0].Name.Should().Be("Nouveau nom", "le nom le plus récent fait foi");
        usage[0].SessionCount.Should().Be(2);
        usage[0].TotalTime.Should().BeCloseTo(TimeSpan.FromHours(2), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void BuildSalonUsage_ShouldNameStandalonePeriods()
    {
        var now = DateTimeOffset.UtcNow;
        var session = SalonSession.Start(null, "peu importe", SalonKind.Standalone, SalonActivationOrigin.Startup,
            now.AddHours(-1));
        session.Close(now);

        var usage = GetStatisticsQueryHandler.BuildSalonUsage([session], now.AddHours(-24), now);

        usage.Single().Name.Should().Be(ActivityLabels.StandaloneSalonName);
    }

    [Fact]
    public void BuildSalonUsage_ShouldSplitTheShareBetweenSalons()
    {
        var now = DateTimeOffset.UtcNow;

        var first = SalonSession.Start(Guid.NewGuid(), "A", SalonKind.Reflector, SalonActivationOrigin.Web, now.AddHours(-4));
        first.Close(now.AddHours(-1));

        var second = SalonSession.Start(Guid.NewGuid(), "B", SalonKind.Reflector, SalonActivationOrigin.Web, now.AddHours(-1));
        second.Close(now);

        var usage = GetStatisticsQueryHandler.BuildSalonUsage([first, second], now.AddHours(-24), now);

        usage[0].Name.Should().Be("A");
        usage[0].SharePercent.Should().BeApproximately(75, 0.5);
        usage[1].SharePercent.Should().BeApproximately(25, 0.5);
    }

    [Fact]
    public void BuildSalonUsage_ShouldDropASessionWithoutOverlap()
    {
        var now = DateTimeOffset.UtcNow;
        var session = SalonSession.Start(Guid.NewGuid(), "TG208", SalonKind.Reflector, SalonActivationOrigin.Web,
            now.AddHours(-30));
        session.Close(now.AddHours(-29));

        var usage = GetStatisticsQueryHandler.BuildSalonUsage([session], now.AddHours(-24), now);

        usage.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Origines
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ShouldSplitActivationsByOrigin()
    {
        var now = DateTimeOffset.UtcNow;
        _repository.GetSessionsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            Closed("A", SalonKind.Reflector, SalonActivationOrigin.Web, now.AddHours(-5), now.AddHours(-4)),
            Closed("B", SalonKind.Reflector, SalonActivationOrigin.Dtmf, now.AddHours(-3), now.AddHours(-2)),
            Closed("C", SalonKind.Reflector, SalonActivationOrigin.Dtmf, now.AddHours(-2), now.AddHours(-1))
        ]);

        var stats = await HandleAsync();

        stats.ActivationOrigins[0].Origin.Should().Be(SalonActivationOrigin.Dtmf);
        stats.ActivationOrigins[0].Count.Should().Be(2);
        stats.ActivationOrigins[0].SharePercent.Should().BeApproximately(66.7, 0.5);
    }

    // -------------------------------------------------------------------------
    // Disponibilité de la liaison
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ShouldReportNoAvailabilityWithoutReflectorSession()
    {
        var now = DateTimeOffset.UtcNow;
        _repository.GetSessionsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            Closed("Perroquet", SalonKind.Parrot, SalonActivationOrigin.Web, now.AddHours(-2), now.AddHours(-1))
        ]);

        var stats = await HandleAsync();

        stats.Reliability.AvailabilityPercent.Should().BeNull();
        stats.Summary.LinkAvailabilityPercent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCountTheStillOpenLinkedPeriod()
    {
        var now = DateTimeOffset.UtcNow;
        _repository.GetSessionsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            Closed("TG208", SalonKind.Reflector, SalonActivationOrigin.Web, now.AddHours(-4), now.AddHours(-2))
        ]);

        // Aucune période de liaison écrite : elle court encore depuis 2 heures.
        _recorder.PendingLinkUpSince.Returns(now.AddHours(-2));

        var stats = await HandleAsync();

        stats.Reliability.LinkedTime.Should().BeCloseTo(TimeSpan.FromHours(2), TimeSpan.FromSeconds(2));
        stats.Reliability.AvailabilityPercent.Should().BeApproximately(100, 1,
            "sans ce rattrapage, un nœud lié en continu afficherait une disponibilité nulle");
    }

    [Fact]
    public async Task Handle_ShouldCapAvailabilityAtOneHundredPercent()
    {
        var now = DateTimeOffset.UtcNow;
        _repository.GetSessionsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            Closed("TG208", SalonKind.Reflector, SalonActivationOrigin.Web, now.AddHours(-2), now.AddHours(-1))
        ]);
        _repository.GetEventSummariesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            // Sessions et logs ne viennent pas de la même source : un dépassement est possible.
            new ActivityEventSummary(ActivityEventType.ReflectorLinkUp, 1, 7200, 7200)
        ]);

        var stats = await HandleAsync();

        stats.Reliability.AvailabilityPercent.Should().Be(100);
    }

    // -------------------------------------------------------------------------
    // Trafic, DTMF, salons inutilisés
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ShouldComputeTheAverageTalkerDuration()
    {
        _repository.GetEventSummariesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            new ActivityEventSummary(ActivityEventType.TalkerHeard, 4, 100, 55)
        ]);

        var stats = await HandleAsync();

        stats.Traffic.Count.Should().Be(4);
        stats.Traffic.AverageTime.Should().Be(TimeSpan.FromSeconds(25));
        stats.Traffic.LongestTime.Should().Be(TimeSpan.FromSeconds(55));
    }

    [Fact]
    public async Task Handle_ShouldListSalonsNeverActivated()
    {
        var used = CreateSalon("TG208");
        var unused = CreateSalon("TG209");
        _salonRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([used, unused]);

        var now = DateTimeOffset.UtcNow;
        _repository.GetSessionsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            SalonSession.Start(used.Id, "TG208", SalonKind.Reflector, SalonActivationOrigin.Web, now.AddHours(-2))
        ]);

        var stats = await HandleAsync();

        stats.UnusedSalonNames.Should().BeEquivalentTo(["TG209"]);
    }

    [Fact]
    public async Task Handle_ShouldClassifyDtmfCodesAgainstTheCurrentSalons()
    {
        var salon = CreateSalon("TG208");
        salon.UpdateDtmfCode(208);
        _salonRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([salon]);

        var now = DateTimeOffset.UtcNow;
        _repository.GetDtmfSummariesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            new DtmfCodeSummary("208", 5, now.AddHours(-1)),
            new DtmfCodeSummary("310", 2, now.AddHours(-2)),
            new DtmfCodeSummary("7777", 3, now.AddHours(-3))
        ]);

        var stats = await HandleAsync();

        stats.Dtmf.TotalCount.Should().Be(10);
        stats.Dtmf.TopCodes[0].Code.Should().Be("208");
        stats.Dtmf.TopCodes[0].Category.Should().Be(DtmfCommandCategory.SalonSwitch);
        stats.Dtmf.UnmatchedCodes.Should().ContainSingle(c => c.Code == "7777");
        stats.Summary.DtmfCount.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ShouldFlagAnEmptyHistory()
    {
        var stats = await HandleAsync();

        stats.IsEmpty.Should().BeTrue();
        stats.HistoryStart.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldWarnWhenSquelchWasNeverObserved()
    {
        _repository.HasAnyEventAsync(ActivityEventType.LocalTransmission, Arg.Any<CancellationToken>())
            .Returns(false);

        var stats = await HandleAsync();

        stats.LocalActivity.IsSquelchTrackingObserved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReportParrotUsageFromSessions()
    {
        var now = DateTimeOffset.UtcNow;
        _repository.GetSessionsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            Closed("Perroquet", SalonKind.Parrot, SalonActivationOrigin.Dtmf, now.AddMinutes(-30), now.AddMinutes(-20)),
            Closed("TG208", SalonKind.Reflector, SalonActivationOrigin.Web, now.AddMinutes(-20), now.AddMinutes(-10))
        ]);

        var stats = await HandleAsync();

        stats.LocalActivity.ParrotSessionCount.Should().Be(1);
        stats.LocalActivity.ParrotTime.Should().BeCloseTo(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(1));
    }

    // -------------------------------------------------------------------------
    // Utilitaires
    // -------------------------------------------------------------------------

    private Task<StatisticsDto> HandleAsync(StatisticsPeriod period = StatisticsPeriod.Last24Hours)
        => new GetStatisticsQueryHandler(
                _repository,
                _salonRepository,
                _recorder,
                Options.Create(new StatisticsOptions()))
            .Handle(new GetStatisticsQuery(period), CancellationToken.None);

    private static SalonSession Closed(
        string name,
        SalonKind kind,
        SalonActivationOrigin origin,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt)
    {
        var session = SalonSession.Start(
            kind == SalonKind.Standalone ? null : Guid.NewGuid(), name, kind, origin, startedAt);
        session.Close(endedAt);
        return session;
    }

    private static SalonAggregate CreateSalon(string name)
        => SalonAggregate.Create(Guid.NewGuid(), name, isDefault: false, CreateConfiguration())
            .Match(
                Succ: aggregate => aggregate,
                Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));

    private static SvxLinkConfiguration CreateConfiguration() => new(
        Guid.NewGuid(),
        Logics: "SimplexLogic,ReflectorLogic",
        CfgDir: "svxlink.d",
        CardSampleRate: 16000,
        CardChannels: 1,
        Host: "ref.f5kri.fr",
        Port: 5300,
        Callsign: "F5ABC-L",
        AuthKey: "test-auth-key-123",
        JitterBufferDelay: 0,
        ReflectorProtocol: ReflectorProtocol.V2,
        CertEmail: null,
        SimplexCallsign: "F5ABC",
        Modules: "ModuleHelp,ModuleParrot",
        ShortIdentInterval: 60,
        LongIdentInterval: 60,
        ReportCtcss: "71.9",
        DefaultLang: "fr_FR",
        RgrSoundDelay: 0,
        RxFrequency: 145.550m,
        TxFrequency: 145.550m,
        RxCtcss: 136.5m,
        TxCtcss: 136.5m);
}
