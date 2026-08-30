using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Audio;
using SvxlinkManagerV2.Application.Features.Audio.GetAudioSettings;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;
using LanguageExtError = LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Application.Tests.Features.Audio;

/// <summary>
/// Tests unitaires de GetAudioSettingsQueryHandler.
/// </summary>
public class GetAudioSettingsQueryHandlerTests
{
    private readonly IAudioService _audioService = Substitute.For<IAudioService>();
    private readonly IPttTestService _pttTestService = Substitute.For<IPttTestService>();
    private readonly IActiveSessionTracker _tracker = Substitute.For<IActiveSessionTracker>();
    private readonly ISvxLinkDaemonService _daemonService = Substitute.For<ISvxLinkDaemonService>();
    private readonly GetAudioSettingsQueryHandler _handler;

    public GetAudioSettingsQueryHandlerTests()
    {
        _audioService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(Mixer());
        _audioService.IsSimulated.Returns(false);

        _pttTestService.State.Returns(PttTestState.Idle(isSimulated: false));
        _pttTestService.DefaultDurationSeconds.Returns(5);
        _pttTestService.MaxDurationSeconds.Returns(30);

        _tracker.ActiveSalonId.Returns(Guid.NewGuid());
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>()).Returns(DaemonRunning(true));

        _handler = new GetAudioSettingsQueryHandler(_audioService, _pttTestService, _tracker, _daemonService);
    }

    [Fact]
    public async Task Handle_ShouldReturnBothLevels()
    {
        var result = await _handler.Handle(new GetAudioSettingsQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto =>
        {
            dto.Capture!.ControlName.Should().Be("ADC Gain");
            dto.Capture.Value.Should().Be(3);
            dto.Capture.MaxValue.Should().Be(7);
            dto.Playback!.ControlName.Should().Be("Line Out");
            dto.Playback.Value.Should().Be(22);
            dto.LevelsError.Should().BeNull();
        });
    }

    [Fact]
    public async Task Handle_ShouldExposeTestDurations()
    {
        var result = await _handler.Handle(new GetAudioSettingsQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto =>
        {
            dto.DefaultTestDurationSeconds.Should().Be(5);
            dto.MaxTestDurationSeconds.Should().Be(30);
        });
    }

    [Fact]
    public async Task Handle_ShouldAllowTheTest_WhenASalonIsActiveAndTheDaemonRuns()
    {
        var result = await _handler.Handle(new GetAudioSettingsQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto =>
        {
            dto.Ptt.CanStart.Should().BeTrue();
            dto.Ptt.BlockedReason.Should().BeNull();
        });
    }

    [Fact]
    public async Task Handle_ShouldBlockTheTest_WhenNoSalonIsActive()
    {
        _tracker.ActiveSalonId.Returns((Guid?)null);

        var result = await _handler.Handle(new GetAudioSettingsQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto =>
        {
            dto.Ptt.CanStart.Should().BeFalse();
            dto.Ptt.BlockedReason.Should().Contain("Aucun salon");
        });
    }

    [Fact]
    public async Task Handle_ShouldBlockTheTest_WhenTheDaemonIsStopped()
    {
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>()).Returns(DaemonRunning(false));

        var result = await _handler.Handle(new GetAudioSettingsQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto =>
        {
            dto.Ptt.CanStart.Should().BeFalse();
            dto.Ptt.BlockedReason.Should().Contain("daemon SVXLink");
        });
    }

    [Fact]
    public async Task Handle_ShouldBlockTheTest_WhileOneIsAlreadyRunning()
    {
        _pttTestService.State.Returns(
            new PttTestState(IsTransmitting: true, DateTimeOffset.UtcNow.AddSeconds(5), IsSimulated: false));

        var result = await _handler.Handle(new GetAudioSettingsQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto =>
        {
            dto.Ptt.IsTransmitting.Should().BeTrue();
            dto.Ptt.CanStart.Should().BeFalse();
        });
    }

    [Fact]
    public async Task Handle_ShouldSucceedWithAnExplanation_WhenTheSoundCardIsUnreadable()
    {
        // Une carte son muette ne doit pas priver l'utilisateur du reste de la page.
        _audioService.GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(Fail<AudioMixerState>("AUDIO_AMIXER_FAILED", "amixer introuvable"));

        var result = await _handler.Handle(new GetAudioSettingsQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto =>
        {
            dto.Capture.Should().BeNull();
            dto.Playback.Should().BeNull();
            dto.LevelsError.Should().Contain("amixer introuvable");
            dto.Ptt.CanStart.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Handle_ShouldReportSimulation_WhenTheMockIsInUse()
    {
        _audioService.IsSimulated.Returns(true);
        _audioService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(Mixer(isSimulated: true));

        var result = await _handler.Handle(new GetAudioSettingsQuery(), CancellationToken.None);

        result.ShouldBeSuccess(dto => dto.IsSimulated.Should().BeTrue());
    }

    private static Validation<Error, AudioMixerState> Mixer(bool isSimulated = false) =>
        new AudioMixerState(
            0,
            new AudioControlState("ADC Gain", 3, 0, 7),
            new AudioControlState("Line Out", 22, 0, 31),
            isSimulated).ToSuccess();

    /// <summary>
    /// L'état du daemon transite par le type d'erreur de LanguageExt, non par celui du domaine.
    /// </summary>
    private static Validation<LanguageExtError, bool> DaemonRunning(bool running) =>
        Validation<LanguageExtError, bool>.Success(running);

    private static Validation<Error, T> Fail<T>(string code, string message)
        => Validation<Error, T>.Fail(Prelude.Seq1(Error.Validation(code, message)));
}
