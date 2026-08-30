using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Audio.UpdateAudioLevels;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Tests.Features.Audio;

/// <summary>
/// Tests unitaires de UpdateAudioLevelsCommandHandler.
/// </summary>
public class UpdateAudioLevelsCommandHandlerTests
{
    private readonly IAudioService _audioService = Substitute.For<IAudioService>();
    private readonly IAudioConfigurationRepository _repository = Substitute.For<IAudioConfigurationRepository>();
    private readonly UpdateAudioLevelsCommandHandler _handler;

    public UpdateAudioLevelsCommandHandlerTests()
    {
        _audioService.SetCaptureLevelAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new AudioControlState("ADC Gain", call.Arg<int>(), 0, 7).ToSuccess());
        _audioService.SetPlaybackLevelAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new AudioControlState("Line Out", call.Arg<int>(), 0, 31).ToSuccess());

        _repository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Fail<AudioConfigurationAggregate>("AUDIOCONFIGURATION_NOT_FOUND", "absente"));
        _repository.SaveAsync(Arg.Any<AudioConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Prelude.unit.ToSuccess());

        _handler = new UpdateAudioLevelsCommandHandler(_audioService, _repository);
    }

    [Fact]
    public async Task Handle_ShouldApplyBothLevelsToTheSoundCard()
    {
        await _handler.Handle(new UpdateAudioLevelsCommand(5, 18), CancellationToken.None);

        await _audioService.Received(1).SetCaptureLevelAsync(5, Arg.Any<CancellationToken>());
        await _audioService.Received(1).SetPlaybackLevelAsync(18, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnTheAppliedLevels()
    {
        var result = await _handler.Handle(new UpdateAudioLevelsCommand(5, 18), CancellationToken.None);

        result.ShouldBeSuccess(levels =>
        {
            levels.Capture.ControlName.Should().Be("ADC Gain");
            levels.Capture.Value.Should().Be(5);
            levels.Playback.Value.Should().Be(18);
        });
    }

    [Fact]
    public async Task Handle_ShouldCreateTheAggregate_WhenNoneExists()
    {
        await _handler.Handle(new UpdateAudioLevelsCommand(5, 18), CancellationToken.None);

        await _repository.Received(1).SaveAsync(
            Arg.Is<AudioConfigurationAggregate>(aggregate =>
                aggregate.Id == AudioConfigurationAggregate.FixedId &&
                aggregate.CaptureLevel == 5 &&
                aggregate.PlaybackLevel == 18),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUpdateTheExistingAggregate()
    {
        var existing = CreateAggregate();
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(existing.ToSuccess());

        await _handler.Handle(new UpdateAudioLevelsCommand(6, 12), CancellationToken.None);

        existing.CaptureLevel.Should().Be(6);
        existing.PlaybackLevel.Should().Be(12);
        await _repository.Received(1).SaveAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPersistTheClampedValue_NotTheRequestedOne()
    {
        // Le pilote a le dernier mot sur la valeur retenue : c'est elle qui doit être mémorisée,
        // sinon la base décrirait un état que le matériel n'a pas.
        _audioService.SetCaptureLevelAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new AudioControlState("ADC Gain", 7, 0, 7).ToSuccess());

        await _handler.Handle(new UpdateAudioLevelsCommand(99, 18), CancellationToken.None);

        await _repository.Received(1).SaveAsync(
            Arg.Is<AudioConfigurationAggregate>(aggregate => aggregate.CaptureLevel == 7),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_AndNotPersist_WhenTheSoundCardRefuses()
    {
        _audioService.SetPlaybackLevelAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Fail<AudioControlState>("AUDIO_AMIXER_FAILED", "contrôle introuvable"));

        var result = await _handler.Handle(new UpdateAudioLevelsCommand(5, 18), CancellationToken.None);

        result.ShouldBeFail(errors =>
            errors.Should().Contain(error => error.Code == "AUDIO_AMIXER_FAILED"));
        await _repository.DidNotReceive().SaveAsync(
            Arg.Any<AudioConfigurationAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPersistenceFails()
    {
        // Le niveau est bien appliqué, mais il ne survivrait pas au redémarrage : l'utilisateur
        // doit le savoir plutôt que de croire son réglage acquis.
        _repository.SaveAsync(Arg.Any<AudioConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Fail<Unit>("SAVE_ERROR", "base verrouillée"));

        var result = await _handler.Handle(new UpdateAudioLevelsCommand(5, 18), CancellationToken.None);

        result.ShouldBeFail(errors => errors.Should().Contain(error => error.Code == "SAVE_ERROR"));
    }

    private static AudioConfigurationAggregate CreateAggregate() =>
        AudioConfigurationAggregate.Create("ADC Gain", 3, "Line Out", 22)
            .Match(
                Succ: aggregate => aggregate,
                Fail: _ => throw new InvalidOperationException("Création attendue en succès."));

    private static Validation<Error, T> Fail<T>(string code, string message)
        => Validation<Error, T>.Fail(Prelude.Seq1(Error.Validation(code, message)));
}
