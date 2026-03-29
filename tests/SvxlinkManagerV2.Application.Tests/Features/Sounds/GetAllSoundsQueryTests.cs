using FluentAssertions;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Sounds.GetAllSounds;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;

namespace SvxlinkManagerV2.Application.Tests.Features.Sounds;

/// <summary>
/// Tests unitaires pour GetAllSoundsQuery et son handler.
/// </summary>
public class GetAllSoundsQueryTests
{
    private readonly ISoundRepository _repository;

    public GetAllSoundsQueryTests()
    {
        _repository = Substitute.For<ISoundRepository>();
    }

    [Fact]
    public async Task Handle_ShouldReturnAllSounds()
    {
        // Arrange
        var sounds = new List<SoundAggregate>
        {
            SoundTestHelpers.CreateValidAggregate(Guid.NewGuid(), "welcome"),
            SoundTestHelpers.CreateValidAggregate(Guid.NewGuid(), "goodbye"),
        };

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(sounds.AsReadOnly());

        var query = new GetAllSoundsQuery();

        // Act
        var result = await new GetAllSoundsQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Name == "welcome");
        result.Should().Contain(s => s.Name == "goodbye");

        await _repository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoSounds_ShouldReturnEmptyList()
    {
        // Arrange
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(System.Array.Empty<SoundAggregate>());

        var query = new GetAllSoundsQuery();

        // Act
        var result = await new GetAllSoundsQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}
