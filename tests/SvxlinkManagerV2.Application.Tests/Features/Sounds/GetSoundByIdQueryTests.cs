using FluentAssertions;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Sounds.GetSoundById;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Tests.Features.Sounds;

/// <summary>
/// Tests unitaires pour GetSoundByIdQuery et son handler.
/// </summary>
public class GetSoundByIdQueryTests
{
    private readonly ISoundRepository _repository;

    public GetSoundByIdQueryTests()
    {
        _repository = Substitute.For<ISoundRepository>();
    }

    [Fact]
    public async Task Handle_WhenSoundExists_ShouldReturnSound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sound = SoundTestHelpers.CreateValidAggregate(id);
        var query = new GetSoundByIdQuery(id);

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(sound.ToSuccess());

        // Act
        var result = await new GetSoundByIdQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess(s =>
        {
            s.Id.Should().Be(id);
            s.Name.Should().Be("test-sound");
        });

        await _repository.Received(1).GetByIdAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSoundNotFound_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var query = new GetSoundByIdQuery(id);

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Sound", id).ToFailure<SoundAggregate>());

        // Act
        var result = await new GetSoundByIdQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });
    }
}
