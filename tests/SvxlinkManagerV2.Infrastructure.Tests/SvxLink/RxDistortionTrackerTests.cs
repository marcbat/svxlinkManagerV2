using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Infrastructure.SvxLink;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests du compteur d'écrêtages de l'audio en réception, alimenté par les logs SVXLink.
/// </summary>
public class RxDistortionTrackerTests
{
    private readonly ISvxLinkLogService _logService = Substitute.For<ISvxLinkLogService>();

    [Fact]
    public void NewTracker_ShouldStartWithoutDetection()
    {
        using var tracker = CreateTracker();

        tracker.DetectionCount.Should().Be(0);
        tracker.LastDetectedAt.Should().BeNull();
    }

    [Fact]
    public void OnLogReceived_ShouldCountTheSvxLinkPeakMeterMessage()
    {
        using var tracker = CreateTracker();

        Emit("Rx1: Distortion detected! Please lower the input volume!");

        tracker.DetectionCount.Should().Be(1);
        tracker.LastDetectedAt.Should().NotBeNull();
    }

    [Fact]
    public void OnLogReceived_ShouldRaiseTheEvent()
    {
        using var tracker = CreateTracker();
        DateTimeOffset? observed = null;
        tracker.OnDistortionDetected += at => observed = at;

        Emit("Rx1: Distortion detected! Please lower the input volume!");

        observed.Should().NotBeNull();
    }

    [Fact]
    public void OnLogReceived_ShouldAccumulateSuccessiveDetections()
    {
        using var tracker = CreateTracker();

        Emit("Rx1: Distortion detected! Please lower the input volume!");
        Emit("Rx1: Distortion detected! Please lower the input volume!");
        Emit("Rx1: Distortion detected! Please lower the input volume!");

        tracker.DetectionCount.Should().Be(3);
    }

    [Fact]
    public void OnLogReceived_ShouldIgnoreUnrelatedLines()
    {
        using var tracker = CreateTracker();

        Emit("Rx1: The squelch is OPEN (0)");
        Emit("SimplexLogic: Loading RX \"Rx1\"");

        tracker.DetectionCount.Should().Be(0);
    }

    [Fact]
    public void Reset_ShouldClearCounterAndTimestamp()
    {
        using var tracker = CreateTracker();
        Emit("Rx1: Distortion detected! Please lower the input volume!");

        tracker.Reset();

        tracker.DetectionCount.Should().Be(0);
        tracker.LastDetectedAt.Should().BeNull();
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromTheLogStream()
    {
        var tracker = CreateTracker();
        tracker.Dispose();

        Emit("Rx1: Distortion detected! Please lower the input volume!");

        tracker.DetectionCount.Should().Be(0);
    }

    private RxDistortionTracker CreateTracker() =>
        new(Substitute.For<ILogger<RxDistortionTracker>>(), _logService);

    /// <summary>
    /// Rejoue une ligne de log auprès des abonnés du service de logs simulé.
    /// </summary>
    private void Emit(string message) =>
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(
            new SvxLinkLogEntry(DateTime.UtcNow, message, SvxLinkLogLevel.Info));
}
