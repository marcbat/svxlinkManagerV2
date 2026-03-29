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
            CreateValidAggregate(Guid.NewGuid(), "welcome"),
            CreateValidAggregate(Guid.NewGuid(), "goodbye"),
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
            .Returns(Array.Empty<SoundAggregate>());

        var query = new GetAllSoundsQuery();

        // Act
        var result = await new GetAllSoundsQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    private static SoundAggregate CreateValidAggregate(Guid id, string name)
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

        var result = SoundAggregate.Create(id, name, wavContent);
        return result.Match(
            Succ: a => { a.ClearDomainEvents(); return a; },
            Fail: _ => throw new InvalidOperationException("Failed to create test aggregate"));
    }
}
