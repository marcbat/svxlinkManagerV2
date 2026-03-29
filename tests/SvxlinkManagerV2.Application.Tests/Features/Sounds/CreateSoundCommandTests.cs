using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Sounds.CreateSound;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Sounds;

/// <summary>
/// Tests unitaires pour CreateSoundCommand et son handler.
/// </summary>
public class CreateSoundCommandTests
{
    private readonly ISoundRepository _repository;

    public CreateSoundCommandTests()
    {
        _repository = Substitute.For<ISoundRepository>();
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateAndReturnId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var command = new CreateSoundCommand(id, "welcome", CreateValidWavFile());

        _repository.SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await new CreateSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess(returnedId =>
        {
            returnedId.Should().Be(id);
        });

        await _repository.Received(1).SaveAsync(
            Arg.Is<SoundAggregate>(a => a.Id == id && a.Name == "welcome"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEmptyName_ShouldFail()
    {
        // Arrange
        var command = new CreateSoundCommand(Guid.NewGuid(), "", CreateValidWavFile());
        // Act
        var result = await new CreateSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_NAME_REQUIRED");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEmptyFileContent_ShouldFail()
    {
        // Arrange
        var command = new CreateSoundCommand(Guid.NewGuid(), "test", System.Array.Empty<byte>());

        // Act
        var result = await new CreateSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_CONTENT_EMPTY");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRepositorySaveFails_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var command = new CreateSoundCommand(id, "welcome", CreateValidWavFile());
        var saveError = Error.Validation("SAVE_ERROR", "Erreur lors de la sauvegarde");

        _repository.SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>())
            .Returns(saveError.ToFailure<Unit>());

        // Act
        var result = await new CreateSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SAVE_ERROR");
        });
    }

    private static byte[] CreateValidWavFile()
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

        return ms.ToArray();
    }
}
