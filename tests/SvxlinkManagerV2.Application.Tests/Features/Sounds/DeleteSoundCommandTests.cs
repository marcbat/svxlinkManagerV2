using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Sounds.DeleteSound;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Sounds;

/// <summary>
/// Tests unitaires pour DeleteSoundCommand et son handler.
/// </summary>
public class DeleteSoundCommandTests
{
    private readonly ISoundRepository _repository;

    public DeleteSoundCommandTests()
    {
        _repository = Substitute.For<ISoundRepository>();
    }

    [Fact]
    public async Task Handle_WhenSoundExists_ShouldDeleteSuccessfully()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sound = CreateValidAggregate(id);
        var command = new DeleteSoundCommand(id);

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(sound.ToSuccess());
        _repository.SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await new DeleteSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        sound.IsDeleted.Should().BeTrue();

        await _repository.Received(1).SaveAsync(
            Arg.Is<SoundAggregate>(a => a.Id == id && a.IsDeleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSoundNotFound_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var command = new DeleteSoundCommand(id);

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Sound", id).ToFailure<SoundAggregate>());

        // Act
        var result = await new DeleteSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSoundAlreadyDeleted_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sound = CreateValidAggregate(id);
        sound.Delete(); // already deleted
        sound.ClearDomainEvents();

        var command = new DeleteSoundCommand(id);

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(sound.ToSuccess());

        // Act
        var result = await new DeleteSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_ALREADY_DELETED");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRepositorySaveFails_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sound = CreateValidAggregate(id);
        var command = new DeleteSoundCommand(id);

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(sound.ToSuccess());
        _repository.SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation("SAVE_ERROR", "Erreur lors de la sauvegarde").ToFailure<Unit>());

        // Act
        var result = await new DeleteSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SAVE_ERROR");
        });
    }

    private static SoundAggregate CreateValidAggregate(Guid id)
    {
        const int sampleRate = 16000;
        const int channels = 1;
        const int durationMs = 100;
        var numSamples = sampleRate * durationMs / 1000;
        var dataSize = numSamples * channels * 2;
        var fileSize = 36 + dataSize;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(fileSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        for (int i = 0; i < numSamples * channels; i++)
            writer.Write((short)0);
        var wavContent = ms.ToArray();

        var result = SoundAggregate.Create(id, "test-sound", wavContent);
        return result.Match(
            Succ: a => { a.ClearDomainEvents(); return a; },
            Fail: _ => throw new InvalidOperationException("Failed to create test aggregate"));
    }
}
