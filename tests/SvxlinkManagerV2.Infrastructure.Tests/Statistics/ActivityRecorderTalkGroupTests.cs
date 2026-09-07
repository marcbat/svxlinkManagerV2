using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Statistics;
using SvxlinkManagerV2.Infrastructure.Statistics;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Statistics;

/// <summary>
/// Tests du suivi du temps passé par talkgroup.
/// </summary>
/// <remarks>
/// Le schéma est celui déjà en place pour la liaison réflecteur : l'intervalle est écrit
/// <b>à sa fin</b>, avec sa durée déjà calculée, et l'intervalle encore ouvert est exposé à
/// part pour que la lecture puisse le rattraper. Un arrêt brutal ne laisse ainsi jamais
/// d'enregistrement à moitié constitué.
/// </remarks>
public class ActivityRecorderTalkGroupTests
{
    private readonly IActivityRepository _repository = Substitute.For<IActivityRepository>();

    private ActivityRecorder CreateRecorder()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _repository);

        return new ActivityRecorder(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ActivityRecorder>.Instance);
    }

    private IReadOnlyList<ActivityEvent> RecordedEvents() =>
        _repository.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IActivityRepository.AddEventAsync))
            .Select(call => (ActivityEvent)call.GetArguments()[0]!)
            .ToList();

    [Fact]
    public async Task RecordTalkGroupAsync_TheFirstTime_ShouldOpenAnIntervalWithoutWriting()
    {
        var recorder = CreateRecorder();

        await recorder.RecordTalkGroupAsync(240);

        recorder.PendingTalkGroup!.Value.TalkGroup.Should().Be(240);
        RecordedEvents().Should().BeEmpty("un intervalle ouvert n'est pas encore un événement");
    }

    [Fact]
    public async Task RecordTalkGroupAsync_OnChange_ShouldCloseThePreviousIntervalWithItsDuration()
    {
        var recorder = CreateRecorder();
        await recorder.RecordTalkGroupAsync(240);

        await recorder.RecordTalkGroupAsync(2403);

        var written = RecordedEvents().Should().ContainSingle().Subject;
        written.Type.Should().Be(ActivityEventType.TalkGroupPeriod);
        written.TalkGroup.Should().Be(240, "c'est le talkgroup quitté qui est comptabilisé");
        written.DurationSeconds.Should().NotBeNull();
        recorder.PendingTalkGroup!.Value.TalkGroup.Should().Be(2403);
    }

    [Fact]
    public async Task RecordTalkGroupAsync_WithTheSameTalkGroup_ShouldChangeNothing()
    {
        var recorder = CreateRecorder();
        await recorder.RecordTalkGroupAsync(240);

        await recorder.RecordTalkGroupAsync(240);

        RecordedEvents().Should().BeEmpty();
    }

    /// <summary>
    /// Le tracker publie <c>null</c> hors protocole V3 : l'intervalle se ferme, et rien ne
    /// s'ouvre. Un salon V2 ne produit donc aucun temps de talkgroup.
    /// </summary>
    [Fact]
    public async Task RecordTalkGroupAsync_WithNull_ShouldCloseTheIntervalWithoutOpeningAnother()
    {
        var recorder = CreateRecorder();
        await recorder.RecordTalkGroupAsync(240);

        await recorder.RecordTalkGroupAsync(null);

        RecordedEvents().Should().ContainSingle().Which.TalkGroup.Should().Be(240);
        recorder.PendingTalkGroup.Should().BeNull();
    }

    [Fact]
    public async Task RecordTalkGroupAsync_WithNullWhileNothingIsOpen_ShouldWriteNothing()
    {
        var recorder = CreateRecorder();

        await recorder.RecordTalkGroupAsync(null);

        RecordedEvents().Should().BeEmpty();
        recorder.PendingTalkGroup.Should().BeNull();
    }

    [Fact]
    public async Task RecordTalkGroupAsync_ShouldTrackTalkGroupZeroLikeAnyOther()
    {
        // 0 signifie « aucun talkgroup », ce qui n'est pas l'absence de la notion : le temps
        // passé hors talkgroup est une information en soi.
        var recorder = CreateRecorder();
        await recorder.RecordTalkGroupAsync(0);

        await recorder.RecordTalkGroupAsync(240);

        RecordedEvents().Should().ContainSingle().Which.TalkGroup.Should().Be(0);
    }

    [Fact]
    public async Task PendingTalkGroup_ShouldExposeWhenTheIntervalStarted()
    {
        var before = DateTimeOffset.UtcNow;
        var recorder = CreateRecorder();

        await recorder.RecordTalkGroupAsync(240);

        recorder.PendingTalkGroup!.Value.Since.Should().BeOnOrAfter(before);
    }
}
