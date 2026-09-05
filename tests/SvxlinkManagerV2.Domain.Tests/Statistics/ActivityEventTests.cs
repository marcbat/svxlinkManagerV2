using FluentAssertions;
using SvxlinkManagerV2.Domain.Statistics;
using Xunit;

namespace SvxlinkManagerV2.Domain.Tests.Statistics;

/// <summary>
/// Tests de <see cref="ActivityEvent"/>.
/// </summary>
public class ActivityEventTests
{
    private static readonly DateTimeOffset Moment = new(2026, 8, 30, 12, 34, 56, TimeSpan.FromHours(2));

    [Fact]
    public void Create_ShouldNormaliseToUtc()
    {
        var activityEvent = ActivityEvent.Create(ActivityEventType.RxDistortion, Moment);

        activityEvent.OccurredAt.Offset.Should().Be(TimeSpan.Zero);
        activityEvent.OccurredAt.Should().Be(Moment.ToUniversalTime());
    }

    [Fact]
    public void Create_ShouldFreezeLocalHourAndDay()
    {
        var activityEvent = ActivityEvent.Create(ActivityEventType.TalkerHeard, Moment);

        // L'heure figée doit correspondre au fuseau de la machine qui exécute le test,
        // quel que soit celui de l'horodatage fourni.
        var local = Moment.ToLocalTime();
        activityEvent.LocalHour.Should().Be(local.Hour);
        activityEvent.LocalDayOfWeek.Should().Be((int)local.DayOfWeek);
    }

    [Fact]
    public void Create_ShouldRoundDurationToSeconds()
    {
        var activityEvent = ActivityEvent.Create(
            ActivityEventType.TalkerHeard,
            Moment,
            duration: TimeSpan.FromMilliseconds(2600));

        activityEvent.DurationSeconds.Should().Be(3);
    }

    [Fact]
    public void Create_ShouldClampNegativeDurationToZero()
    {
        var activityEvent = ActivityEvent.Create(
            ActivityEventType.ReflectorLinkUp,
            Moment,
            duration: TimeSpan.FromSeconds(-10));

        activityEvent.DurationSeconds.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldLeaveDurationUnsetWhenNoneGiven()
    {
        var activityEvent = ActivityEvent.Create(ActivityEventType.DtmfCommand, Moment, detail: "310");

        activityEvent.DurationSeconds.Should().BeNull();
        activityEvent.Detail.Should().Be("310");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldTreatBlankTextAsAbsent(string blank)
    {
        var activityEvent = ActivityEvent.Create(
            ActivityEventType.TalkerHeard,
            Moment,
            callsign: blank,
            detail: blank);

        activityEvent.Callsign.Should().BeNull();
        activityEvent.Detail.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimCallsign()
    {
        var activityEvent = ActivityEvent.Create(ActivityEventType.TalkerHeard, Moment, callsign: "  HB9GXP-H  ");

        activityEvent.Callsign.Should().Be("HB9GXP-H");
    }
}
