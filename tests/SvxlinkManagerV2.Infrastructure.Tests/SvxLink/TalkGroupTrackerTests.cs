using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests du suivi du talkgroup courant à partir des logs SVXLink.
/// La ligne utilisée est celle réellement émise par <c>ReflectorLogic::selectTg</c> en 25.05.
/// </summary>
public class TalkGroupTrackerTests
{
    private readonly ILogger<TalkGroupTracker> _logger = Substitute.For<ILogger<TalkGroupTracker>>();
    private readonly ISvxLinkLogService _logService = Substitute.For<ISvxLinkLogService>();

    private TalkGroupTracker CreateTracker() => new(_logger, _logService);

    private void Log(string message) =>
        _logService.OnLogReceived += Raise.Event<Action<SvxLinkLogEntry>>(
            new SvxLinkLogEntry(DateTime.Now, message, SvxLinkLogLevel.Info));

    [Fact]
    public void Current_ShouldBeNull_BeforeAnySalonIsActivated()
    {
        using var tracker = CreateTracker();

        tracker.Current.Should().BeNull();
    }

    [Fact]
    public void ApplyDefault_ShouldPublishTheSalonDefaultTalkGroup()
    {
        using var tracker = CreateTracker();
        int? published = null;
        tracker.OnTalkGroupChanged += tg => published = tg;

        tracker.ApplyDefault(2403);

        tracker.Current.Should().Be(2403);
        published.Should().Be(2403);
    }

    [Fact]
    public void SelectingTgLine_ShouldUpdateTheCurrentTalkGroup()
    {
        using var tracker = CreateTracker();
        tracker.ApplyDefault(2403);
        int? published = null;
        tracker.OnTalkGroupChanged += tg => published = tg;

        Log("ReflectorLogic: Selecting TG #240");

        tracker.Current.Should().Be(240);
        published.Should().Be(240);
    }

    [Fact]
    public void SelectingTgZero_ShouldBeFollowed()
    {
        // TG_SELECT_TIMEOUT écoulé : SVXLink relâche le talkgroup. L'interface doit le dire
        // plutôt que d'afficher indéfiniment le dernier TG demandé.
        using var tracker = CreateTracker();
        tracker.ApplyDefault(2403);

        Log("ReflectorLogic: Selecting TG #0");

        tracker.Current.Should().Be(0);
    }

    [Fact]
    public void SameTalkGroupTwice_ShouldNotRepublish()
    {
        using var tracker = CreateTracker();
        tracker.ApplyDefault(240);
        var count = 0;
        tracker.OnTalkGroupChanged += _ => count++;

        Log("ReflectorLogic: Selecting TG #240");

        count.Should().Be(0);
    }

    [Fact]
    public void MarkNotApplicable_ShouldClearAndIgnoreSubsequentLines()
    {
        using var tracker = CreateTracker();
        tracker.ApplyDefault(2403);

        tracker.MarkNotApplicable();
        Log("ReflectorLogic: Selecting TG #240");

        tracker.Current.Should().BeNull(
            "un salon V2, un perroquet ou le mode autonome n'ont pas de talkgroup : " +
            "les lignes résiduelles du daemon ne doivent pas en faire apparaître un");
    }

    [Fact]
    public void LinesWithoutTheLogicName_ShouldBeIgnored()
    {
        using var tracker = CreateTracker();
        tracker.ApplyDefault(2403);

        Log("Selecting TG #240");
        Log("SimplexLogic: digit=3");

        tracker.Current.Should().Be(2403);
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromTheLogService()
    {
        var tracker = CreateTracker();

        tracker.Dispose();

        _logService.Received(1).OnLogReceived -= Arg.Any<Action<SvxLinkLogEntry>>();
    }
}
