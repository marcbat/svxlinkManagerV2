using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Infrastructure.SvxLink;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests du suivi du squelch local, alimenté par les logs SVXLink.
/// </summary>
public class SquelchStateTrackerTests
{
    private readonly ISvxLinkLogService _logService = Substitute.For<ISvxLinkLogService>();

    [Fact]
    public void NewTracker_ShouldStartClosed()
    {
        using var tracker = CreateTracker();

        tracker.IsOpen.Should().BeFalse();
    }

    [Theory]
    // Le nom du récepteur et le niveau entre parenthèses varient d'une installation à l'autre.
    [InlineData("Rx1: The squelch is OPEN (12.3)")]
    [InlineData("RxLocal: The squelch is OPEN (0.0)")]
    [InlineData("Rx1: the squelch is open (5)")]
    public void OnLogReceived_ShouldDetectAnOpening(string message)
    {
        using var tracker = CreateTracker();

        Emit(message);

        tracker.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void OnLogReceived_ShouldRaiseTheOpeningEvent()
    {
        using var tracker = CreateTracker();
        DateTimeOffset? observed = null;
        tracker.OnSquelchOpened += at => observed = at;

        Emit("Rx1: The squelch is OPEN (12.3)");

        observed.Should().NotBeNull();
    }

    [Fact]
    public void OnLogReceived_ShouldMeasureTheOpenDuration()
    {
        using var tracker = CreateTracker();
        TimeSpan? duration = null;
        tracker.OnSquelchClosed += d => duration = d;

        Emit("Rx1: The squelch is OPEN (12.3)");
        Emit("Rx1: The squelch is CLOSED (-1.2)");

        tracker.IsOpen.Should().BeFalse();
        duration.Should().NotBeNull();
        duration!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void OnLogReceived_ShouldIgnoreAClosureWithoutOpening()
    {
        // L'application a démarré alors que le squelch était déjà ouvert : aucune durée
        // n'est mesurable, mieux vaut ne rien compter qu'inventer un passage.
        using var tracker = CreateTracker();
        var raised = 0;
        tracker.OnSquelchClosed += _ => raised++;

        Emit("Rx1: The squelch is CLOSED (-1.2)");

        raised.Should().Be(0);
    }

    [Fact]
    public void OnLogReceived_ShouldKeepTheFirstOpeningWhenRepeated()
    {
        using var tracker = CreateTracker();
        var openings = 0;
        tracker.OnSquelchOpened += _ => openings++;

        Emit("Rx1: The squelch is OPEN (12.3)");
        Emit("Rx2: The squelch is OPEN (8.0)");

        openings.Should().Be(1);
        tracker.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void OnLogReceived_ShouldCountEachCompleteCycle()
    {
        using var tracker = CreateTracker();
        var closures = 0;
        tracker.OnSquelchClosed += _ => closures++;

        Emit("Rx1: The squelch is OPEN (12.3)");
        Emit("Rx1: The squelch is CLOSED (-1.2)");
        Emit("Rx1: The squelch is OPEN (10.0)");
        Emit("Rx1: The squelch is CLOSED (-2.0)");

        closures.Should().Be(2);
    }

    [Fact]
    public void OnLogReceived_ShouldIgnoreUnrelatedLines()
    {
        using var tracker = CreateTracker();

        Emit("ReflectorLogic: Talker start: HB9GXP-H");
        Emit("Rx1: Distortion detected! Please lower the input volume!");

        tracker.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromTheLogStream()
    {
        var tracker = CreateTracker();
        tracker.Dispose();

        Emit("Rx1: The squelch is OPEN (12.3)");

        tracker.IsOpen.Should().BeFalse();
    }

    private SquelchStateTracker CreateTracker() =>
        new(Substitute.For<ILogger<SquelchStateTracker>>(), _logService);

    /// <summary>Rejoue une ligne de log auprès des abonnés du service de logs simulé.</summary>
    private void Emit(string message) =>
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(
            new SvxLinkLogEntry(DateTime.UtcNow, message, SvxLinkLogLevel.Info));
}
