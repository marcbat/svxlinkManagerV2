using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Sounds.UpdateSound;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Sounds;

/// <summary>
/// Tests unitaires pour UpdateSoundCommand et son handler.
/// </summary>
public class UpdateSoundCommandTests
{
    private readonly ISoundRepository _repository;

    public UpdateSoundCommandTests()
    {
        _repository = Substitute.For<ISoundRepository>();
    }

    [Fact]
    public async Task Handle_WithNewName_ShouldUpdateSuccessfully()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sound = SoundTestHelpers.CreateValidAggregate(id);
        var command = new UpdateSoundCommand(id, Name: "nouveau-nom");

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(sound.ToSuccess());
        _repository.SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await new UpdateSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        sound.Name.Should().Be("nouveau-nom");

        await _repository.Received(1).SaveAsync(
            Arg.Is<SoundAggregate>(a => a.Id == id && a.Name == "nouveau-nom"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNewFileContent_ShouldUpdateSuccessfully()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sound = SoundTestHelpers.CreateValidAggregate(id);
        var newContent = SoundTestHelpers.CreateValidWavFile(sampleRate: 8000);
        var command = new UpdateSoundCommand(id, FileContent: newContent);

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(sound.ToSuccess());
        _repository.SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await new UpdateSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        sound.SampleRate.Should().Be(8000);

        await _repository.Received(1).SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSoundNotFound_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var command = new UpdateSoundCommand(id, Name: "nouveau-nom");

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Sound", id).ToFailure<SoundAggregate>());

        // Act
        var result = await new UpdateSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSoundIsDeleted_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sound = SoundTestHelpers.CreateValidAggregate(id);
        sound.Delete();
        sound.ClearDomainEvents();

        var command = new UpdateSoundCommand(id, Name: "nouveau-nom");

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(sound.ToSuccess());

        // Act
        var result = await new UpdateSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_DELETED");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRepositorySaveFails_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sound = SoundTestHelpers.CreateValidAggregate(id);
        var command = new UpdateSoundCommand(id, Name: "nouveau-nom");

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(sound.ToSuccess());
        _repository.SaveAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation("SAVE_ERROR", "Erreur lors de la sauvegarde").ToFailure<Unit>());

        // Act
        var result = await new UpdateSoundCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SAVE_ERROR");
        });
    }
}
