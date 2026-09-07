using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests du suivi des talkgroups à partir du flux de logs SVXLink.
/// </summary>
/// <remarks>
/// Les lignes <c>TG_EVENT:</c> sont celles relevées sur la stack Docker le 07/09/2026, en
/// composant les commandes correspondantes dans le PTY de <c>svxlink-node3</c>. La ligne de
/// repli <c>Selecting TG #</c> est celle du C++ de SVXLink 25.05.
/// </remarks>
public class TalkGroupTrackerTests
{
    private readonly ILogger<TalkGroupTracker> _logger = Substitute.For<ILogger<TalkGroupTracker>>();
    private readonly ISvxLinkLogService _logService = Substitute.For<ISvxLinkLogService>();

    private TalkGroupTracker CreateTracker() => new(_logger, _logService);

    private void Log(string message) =>
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(
            new SvxLinkLogEntry(DateTime.Now, message, SvxLinkLogLevel.Info));

    /// <summary>Tracker armé sur un salon V3, prêt à recevoir des événements.</summary>
    private TalkGroupTracker CreateActiveTracker(int defaultTg = 0)
    {
        var tracker = CreateTracker();
        tracker.ApplyDefault(defaultTg);
        return tracker;
    }

    #region État initial et neutralisation

    [Fact]
    public void State_ShouldBeNotApplicable_BeforeAnySalonIsActivated()
    {
        using var tracker = CreateTracker();

        tracker.State.Should().Be(TalkGroupState.NotApplicable);
        tracker.Current.Should().BeNull();
        tracker.State.IsApplicable.Should().BeFalse();
    }

    [Fact]
    public void ApplyDefault_ShouldPublishTheSalonDefaultTalkGroup()
    {
        using var tracker = CreateTracker();
        TalkGroupState? published = null;
        tracker.OnTalkGroupChanged += state => published = state;

        tracker.ApplyDefault(2403);

        tracker.Current.Should().Be(2403);
        published!.TalkGroup.Should().Be(2403);
        published.Origin.Should().Be(TalkGroupActivationOrigin.Default);
    }

    [Fact]
    public void ApplyDefault_ShouldForgetTheTemporaryMonitorsOfThePreviousSalon()
    {
        // Elles appartiennent au daemon qui s'arrête : les conserver ferait afficher des
        // surveillances que SVXLink n'a plus.
        using var tracker = CreateActiveTracker();
        Log("TG_EVENT:monitor_add:2405");

        tracker.ApplyDefault(2403);

        tracker.State.TemporaryMonitors.Should().BeEmpty();
    }

    [Fact]
    public void MarkNotApplicable_ShouldClearAndIgnoreSubsequentLines()
    {
        using var tracker = CreateActiveTracker(2403);

        tracker.MarkNotApplicable();
        Log("TG_EVENT:selected:240:2403");
        Log("ReflectorLogic: Selecting TG #240");

        tracker.State.Should().Be(TalkGroupState.NotApplicable);
    }

    /// <summary>
    /// SVXLink 19.09.2 ignore les talkgroups : ses procédures d'événement n'existent pas et
    /// aucune ligne TG_EVENT n'est jamais émise. Un salon V2 est neutralisé à l'activation,
    /// et rien ne doit pouvoir le réarmer.
    /// </summary>
    [Fact]
    public void V2Salon_ShouldNeverShowATalkGroup()
    {
        using var tracker = CreateTracker();
        tracker.MarkNotApplicable();

        Log("ReflectorLogic: Connected nodes: HB9GXP2-H");
        Log("ReflectorLogic: Selecting TG #240");

        tracker.Current.Should().BeNull();
    }

    #endregion

    #region Événements TCL

    [Fact]
    public void SelectedEvent_ShouldUpdateTheCurrentAndPreviousTalkGroups()
    {
        using var tracker = CreateActiveTracker();

        Log("TG_EVENT:selected:240:0");

        tracker.State.TalkGroup.Should().Be(240);
        tracker.State.PreviousTalkGroup.Should().Be(0);
    }

    [Theory]
    [InlineData("local", TalkGroupActivationOrigin.Local)]
    [InlineData("remote", TalkGroupActivationOrigin.Remote)]
    [InlineData("priority", TalkGroupActivationOrigin.Priority)]
    [InlineData("command", TalkGroupActivationOrigin.Command)]
    [InlineData("default", TalkGroupActivationOrigin.Default)]
    [InlineData("timeout", TalkGroupActivationOrigin.Timeout)]
    public void ActivationEvent_ShouldCarryItsOrigin(string marker, TalkGroupActivationOrigin expected)
    {
        using var tracker = CreateActiveTracker();

        Log($"TG_EVENT:activation:{marker}:240:0");

        tracker.State.TalkGroup.Should().Be(240);
        tracker.State.Origin.Should().Be(expected);
    }

    [Fact]
    public void UnknownActivationOrigin_ShouldNotLoseTheTalkGroup()
    {
        // Une version ultérieure de SVXLink peut ajouter une procédure d'activation :
        // mieux vaut un talkgroup juste sans origine qu'un événement ignoré.
        using var tracker = CreateActiveTracker();

        Log("TG_EVENT:activation:something_new:240:0");

        tracker.State.TalkGroup.Should().Be(240);
        tracker.State.Origin.Should().Be(TalkGroupActivationOrigin.Unknown);
    }

    /// <summary>
    /// Séquence réelle d'un QSY automatique, relevée après <c>AUTO_QSY_AFTER</c> sur TG 2403.
    /// </summary>
    [Fact]
    public void QsyEvent_ShouldBeObserved()
    {
        using var tracker = CreateActiveTracker();
        Log("TG_EVENT:selected:240:0");

        Log("TG_EVENT:selected:2409900:240");
        Log("TG_EVENT:qsy:2409900:240");

        tracker.State.TalkGroup.Should().Be(2409900);
        tracker.State.PreviousTalkGroup.Should().Be(240);
        tracker.State.Origin.Should().Be(TalkGroupActivationOrigin.Qsy);
    }

    [Fact]
    public void PendingQsy_ShouldBeExposedThenClearedWhenFollowed()
    {
        using var tracker = CreateActiveTracker();

        Log("TG_EVENT:qsy_pending:2409900");
        var pending = tracker.State.PendingQsy;

        Log("TG_EVENT:qsy:2409900:240");

        pending.Should().Be(2409900);
        tracker.State.PendingQsy.Should().BeNull();
    }

    [Fact]
    public void IgnoredQsy_ShouldClearThePendingRequest()
    {
        using var tracker = CreateActiveTracker();
        Log("TG_EVENT:qsy_pending:2409900");

        Log("TG_EVENT:qsy_ignored:2409900");

        tracker.State.PendingQsy.Should().BeNull();
    }

    [Fact]
    public void FailedQsy_ShouldBeReported()
    {
        using var tracker = CreateActiveTracker();
        Log("TG_EVENT:qsy_pending:2409900");

        Log("TG_EVENT:qsy_failed");

        tracker.State.LastQsyFailed.Should().BeTrue();
        tracker.State.PendingQsy.Should().BeNull();
    }

    [Fact]
    public void ASuccessfulSelection_ShouldClearAPreviousQsyFailure()
    {
        using var tracker = CreateActiveTracker();
        Log("TG_EVENT:qsy_failed");

        Log("TG_EVENT:selected:240:0");

        tracker.State.LastQsyFailed.Should().BeFalse();
    }

    [Fact]
    public void TemporaryMonitors_ShouldBeTrackedAndOrdered()
    {
        using var tracker = CreateActiveTracker();

        Log("TG_EVENT:monitor_add:2405");
        Log("TG_EVENT:monitor_add:240");

        tracker.State.TemporaryMonitors.Should().Equal(240, 2405);
    }

    [Fact]
    public void TemporaryMonitors_ShouldBeRemovedWhenTheyExpire()
    {
        using var tracker = CreateActiveTracker();
        Log("TG_EVENT:monitor_add:2405");
        Log("TG_EVENT:monitor_add:240");

        Log("TG_EVENT:monitor_remove:2405");

        tracker.State.TemporaryMonitors.Should().Equal(240);
    }

    [Fact]
    public void AddingTheSameTemporaryMonitorTwice_ShouldNotDuplicateIt()
    {
        using var tracker = CreateActiveTracker();
        var count = 0;
        tracker.OnTalkGroupChanged += _ => count++;

        Log("TG_EVENT:monitor_add:2405");
        Log("TG_EVENT:monitor_add:2405");

        tracker.State.TemporaryMonitors.Should().Equal(2405);
        count.Should().Be(1);
    }

    [Fact]
    public void EventsAreEmbeddedInLogLines_ShouldStillBeRecognised()
    {
        // Le flux de logs porte un horodatage et le nom du service : la ligne n'arrive
        // jamais nue.
        using var tracker = CreateActiveTracker();

        Log("Sep 07 09:12:33 svxlink[42]: TG_EVENT:selected:2403:0");

        tracker.State.TalkGroup.Should().Be(2403);
    }

    [Theory]
    [InlineData("TG_EVENT:")]
    [InlineData("TG_EVENT:selected")]
    [InlineData("TG_EVENT:selected:abc:0")]
    [InlineData("TG_EVENT:monitor_add")]
    [InlineData("TG_EVENT:inconnu:1:2")]
    public void MalformedEvents_ShouldBeIgnored(string message)
    {
        using var tracker = CreateActiveTracker(2403);

        Log(message);

        tracker.State.TalkGroup.Should().Be(2403);
        tracker.State.TemporaryMonitors.Should().BeEmpty();
    }

    #endregion

    #region Repli sur le message du C++

    /// <summary>
    /// Nœud dont le Logic.tcl n'a pas encore été redéployé : aucune ligne TG_EVENT n'arrive,
    /// mais le message du C++ suffit à connaître le talkgroup.
    /// </summary>
    [Fact]
    public void SelectingTgLine_ShouldStillUpdateTheCurrentTalkGroup()
    {
        using var tracker = CreateActiveTracker(2403);

        Log("ReflectorLogic: Selecting TG #240");

        tracker.State.TalkGroup.Should().Be(240);
        tracker.State.PreviousTalkGroup.Should().Be(2403);
        tracker.State.Origin.Should().Be(TalkGroupActivationOrigin.Unknown);
    }

    [Fact]
    public void SelectingTgZero_ShouldBeFollowed()
    {
        // TG_SELECT_TIMEOUT écoulé : SVXLink relâche le talkgroup. L'interface doit le dire
        // plutôt que d'afficher indéfiniment le dernier TG demandé.
        using var tracker = CreateActiveTracker(2403);

        Log("ReflectorLogic: Selecting TG #0");

        tracker.State.TalkGroup.Should().Be(0);
    }

    [Fact]
    public void SameTalkGroupTwice_ShouldNotRepublish()
    {
        using var tracker = CreateActiveTracker(240);
        var count = 0;
        tracker.OnTalkGroupChanged += _ => count++;

        Log("ReflectorLogic: Selecting TG #240");

        count.Should().Be(0);
    }

    [Fact]
    public void LinesWithoutTheLogicName_ShouldBeIgnored()
    {
        using var tracker = CreateActiveTracker(2403);

        Log("Selecting TG #240");
        Log("SimplexLogic: digit=3");

        tracker.State.TalkGroup.Should().Be(2403);
    }

    #endregion

    [Fact]
    public void Dispose_ShouldUnsubscribeFromTheLogService()
    {
        var tracker = CreateTracker();

        tracker.Dispose();

        _logService.Received(1).OnLogReceived -= Arg.Any<Action<SvxLinkLogEntry>>();
    }
}
