using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests du parsing des lignes de log SVXLink pilotant l'état de la liaison au réflecteur.
/// Les lignes utilisées sont celles réellement émises par ReflectorLogic en 19.09.2 et 25.05.
/// </summary>
public class ReflectorLinkStateTrackerTests
{
    private readonly ILogger<ReflectorLinkStateTracker> _logger;
    private readonly ISvxLinkLogService _logService;

    public ReflectorLinkStateTrackerTests()
    {
        _logger = Substitute.For<ILogger<ReflectorLinkStateTracker>>();
        _logService = Substitute.For<ISvxLinkLogService>();
    }

    private ReflectorLinkStateTracker CreateTracker() => new(_logger, _logService);

    private void Log(string message) =>
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(
            new SvxLinkLogEntry(DateTime.Now, message, SvxLinkLogLevel.Info));

    [Fact]
    public void Constructor_ShouldSubscribeToLogService()
    {
        _ = CreateTracker();

        _logService.Received(1).OnLogReceived += Arg.Any<Action<SvxLinkLogEntry>>();
    }

    [Fact]
    public void State_Initially_ShouldBeInactive()
    {
        var tracker = CreateTracker();

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Inactive);
        tracker.State.Reason.Should().Be(ReflectorLinkFailureReason.None);
    }

    [Fact]
    public void BeginConnecting_ShouldRaiseStateChanged()
    {
        var tracker = CreateTracker();
        ReflectorLinkState? captured = null;
        tracker.OnStateChanged += state => captured = state;

        tracker.BeginConnecting();

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Connecting);
        captured!.Status.Should().Be(ReflectorLinkStatus.Connecting);
    }

    [Fact]
    public void MarkNotApplicable_ShouldReportNoLinkExpected()
    {
        var tracker = CreateTracker();

        tracker.MarkNotApplicable();

        tracker.State.Status.Should().Be(ReflectorLinkStatus.NotApplicable);
    }

    [Theory]
    [InlineData("ReflectorLogic: Connecting to 10.0.0.1:5300")]
    [InlineData("ReflectorLogic: Connecting to service _svxreflector._tcp.example.org")]
    [InlineData("ReflectorLogic: Connection established to 10.0.0.1:5300 (primary)")]
    [InlineData("ReflectorLogic: Encrypted connection established")]
    [InlineData("ReflectorLogic: Authentication OK")]
    public void ConnectionAttemptLines_ShouldReportConnecting(string message)
    {
        var tracker = CreateTracker();

        Log(message);

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Connecting);
    }

    [Fact]
    public void ConnectedNodesLine_ShouldReportConnectedWithoutUserAction()
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();

        Log("ReflectorLogic: Connected nodes: HB9GXP2-H, HB9GXP-H");

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Connected);
        tracker.State.Reason.Should().Be(ReflectorLinkFailureReason.None);
    }

    [Fact]
    public void ConnectedNodesLine_WithEmptyList_ShouldStillReportConnected()
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();

        Log("ReflectorLogic: Connected nodes: ");

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Connected);
    }

    [Theory]
    // 19.09.2 : le refus du réflecteur arrive dans un MsgError relayé sur stdout
    [InlineData("ReflectorLogic: Error message received from server: Access denied")]
    [InlineData("ReflectorLogic: Error message received from server: Invalid callsign")]
    // 25.05 : même message, formaté en erreur
    [InlineData("*** ERROR[ReflectorLogic]: Server error: Access denied")]
    public void ServerRefusal_ShouldReportAuthenticationRejectedWithCause(string message)
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();

        Log(message);

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Failed);
        tracker.State.Reason.Should().Be(ReflectorLinkFailureReason.AuthenticationRejected);
        tracker.State.Detail.Should().Be(message);
    }

    [Theory]
    [InlineData("ReflectorLogic: Disconnected from 10.0.0.1:5300: Host not found")]
    [InlineData("ReflectorLogic: Disconnected from 10.0.0.1:5300: Connection refused")]
    [InlineData("ReflectorLogic: Disconnected from 10.0.0.1:5300: No route to host")]
    [InlineData("ReflectorLogic: Disconnected from 10.0.0.1:5300: Connection timed out")]
    public void UnreachableHost_ShouldReportFailedWithHostUnreachable(string message)
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();

        Log(message);

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Failed);
        tracker.State.Reason.Should().Be(ReflectorLinkFailureReason.HostUnreachable);
        tracker.State.Detail.Should().Be(message);
    }

    [Theory]
    [InlineData("*** ERROR[ReflectorLogic]: The client certificate received from the server does not match our current private key.")]
    [InlineData("ReflectorLogic: Failed to load client certificate.")]
    [InlineData("ReflectorLogic: Received an empty certificate.")]
    [InlineData("*** ERROR[ReflectorLogic]: Failed to parse certificate PEM data from server")]
    public void CertificateProblem_ShouldReportCertificateRejected(string message)
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();

        Log(message);

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Failed);
        tracker.State.Reason.Should().Be(ReflectorLinkFailureReason.CertificateRejected);
    }

    [Fact]
    public void MissingConfiguration_ShouldReportConfigurationInvalid()
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();

        Log("*** ERROR: ReflectorLogic/HOST missing in configuration");

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Failed);
        tracker.State.Reason.Should().Be(ReflectorLinkFailureReason.ConfigurationInvalid);
    }

    [Fact]
    public void LinkLostAfterConnection_ShouldReportDisconnected()
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();
        Log("ReflectorLogic: Connected nodes: HB9GXP-H");

        Log("ReflectorLogic: Disconnected from 10.0.0.1:5300: Connection closed by remote peer");

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Disconnected);
        tracker.State.Reason.Should().Be(ReflectorLinkFailureReason.RemoteDisconnected);
    }

    [Theory]
    [InlineData("ReflectorLogic: Heartbeat timeout")]
    [InlineData("ReflectorLogic: UDP Heartbeat timeout")]
    public void HeartbeatTimeoutAfterConnection_ShouldReportDisconnected(string message)
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();
        Log("ReflectorLogic: Connected nodes: HB9GXP-H");

        Log(message);

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Disconnected);
        tracker.State.Reason.Should().Be(ReflectorLinkFailureReason.HeartbeatTimeout);
    }

    [Fact]
    public void OrderedDisconnectAfterRefusal_ShouldKeepDetectedCause()
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();
        Log("ReflectorLogic: Error message received from server: Access denied");

        // SVXLink ferme lui-même la connexion juste après le refus.
        Log("ReflectorLogic: Disconnected from 10.0.0.1:5300: Locally ordered disconnect");

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Failed);
        tracker.State.Reason.Should().Be(ReflectorLinkFailureReason.AuthenticationRejected);
    }

    [Fact]
    public void AutomaticReconnectAfterRefusal_ShouldKeepLastCauseVisible()
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();
        Log("ReflectorLogic: Error message received from server: Access denied");

        // Le timer de reconnexion de SVXLink relance une tentative en boucle.
        Log("ReflectorLogic: Connecting to 10.0.0.1:5300");

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Connecting);
        tracker.State.Reason.Should().Be(ReflectorLinkFailureReason.AuthenticationRejected);
    }

    [Fact]
    public void SuccessfulReconnect_ShouldClearPreviousCause()
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();
        Log("ReflectorLogic: Error message received from server: Access denied");
        Log("ReflectorLogic: Connecting to 10.0.0.1:5300");

        Log("ReflectorLogic: Connected nodes: HB9GXP-H");

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Connected);
        tracker.State.Reason.Should().Be(ReflectorLinkFailureReason.None);
        tracker.State.Detail.Should().BeNull();
    }

    [Fact]
    public void StandaloneMode_ShouldIgnoreReflectorLogLines()
    {
        var tracker = CreateTracker();
        tracker.MarkNotApplicable();
        var changes = 0;
        tracker.OnStateChanged += _ => changes++;

        Log("ReflectorLogic: Disconnected from 10.0.0.1:5300: Host not found");
        Log("ReflectorLogic: Error message received from server: Access denied");

        tracker.State.Status.Should().Be(ReflectorLinkStatus.NotApplicable);
        changes.Should().Be(0);
    }

    [Theory]
    [InlineData("SimplexLogic: The squelch is OPEN")]
    [InlineData("Rx1: The squelch is CLOSED")]
    [InlineData("DTMF_CMD:310")]
    public void NonReflectorLines_ShouldNotChangeState(string message)
    {
        var tracker = CreateTracker();
        tracker.BeginConnecting();
        Log("ReflectorLogic: Connected nodes: HB9GXP-H");

        Log(message);

        tracker.State.Status.Should().Be(ReflectorLinkStatus.Connected);
    }

    [Fact]
    public void RepeatedLines_ShouldRaiseStateChangedOnlyOnTransition()
    {
        var tracker = CreateTracker();
        var changes = 0;
        tracker.OnStateChanged += _ => changes++;

        Log("ReflectorLogic: Connecting to 10.0.0.1:5300");
        Log("ReflectorLogic: Connection established to 10.0.0.1:5300");
        Log("ReflectorLogic: Connected nodes: HB9GXP-H");
        Log("ReflectorLogic: Connected nodes: HB9GXP-H, HB9GXP2-H");

        changes.Should().Be(2);
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromLogService()
    {
        var tracker = CreateTracker();

        tracker.Dispose();

        _logService.Received(1).OnLogReceived -= Arg.Any<Action<SvxLinkLogEntry>>();
    }
}
