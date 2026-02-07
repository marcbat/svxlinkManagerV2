using FluentAssertions;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Aggregates.Sound.Events;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.Sound;

/// <summary>
/// Tests unitaires pour SoundAggregate
/// </summary>
public class SoundAggregateTests
{
    #region Factory Create Tests

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "welcome";
        var fileContent = CreateValidWavFile();

        // Act
        var result = SoundAggregate.Create(id, name, fileContent);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.Id.Should().Be(id);
            aggregate.Name.Should().Be(name);
            aggregate.FileContent.Should().BeEquivalentTo(fileContent);
            aggregate.Duration.Should().BeGreaterThan(TimeSpan.Zero);
            aggregate.SampleRate.Should().Be(16000);
            aggregate.Channels.Should().Be(1);
            aggregate.IsDeleted.Should().BeFalse();
            aggregate.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<SoundCreatedEvent>();
        });
    }

    [Fact]
    public void Create_WithEmptyId_ShouldFail()
    {
        // Arrange
        var id = Guid.Empty;
        var name = "test";
        var fileContent = CreateValidWavFile();

        // Act
        var result = SoundAggregate.Create(id, name, fileContent);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("EMPTY_ID"));
        });
    }

    [Fact]
    public void Create_WithEmptyName_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "";
        var fileContent = CreateValidWavFile();

        // Act
        var result = SoundAggregate.Create(id, name, fileContent);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_NAME_REQUIRED");
        });
    }

    [Fact]
    public void Create_WithNameTooLong_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = new string('a', 101); // 101 characters
        var fileContent = CreateValidWavFile();

        // Act
        var result = SoundAggregate.Create(id, name, fileContent);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_NAME_TOO_LONG");
        });
    }

    [Fact]
    public void Create_WithInvalidCharactersInName_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "test<>|file"; // Invalid filename characters
        var fileContent = CreateValidWavFile();

        // Act
        var result = SoundAggregate.Create(id, name, fileContent);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_NAME_INVALID_CHARS");
        });
    }

    [Fact]
    public void Create_WithEmptyFileContent_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "test";
        var fileContent = System.Array.Empty<byte>();

        // Act
        var result = SoundAggregate.Create(id, name, fileContent);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_CONTENT_EMPTY");
        });
    }

    [Fact]
    public void Create_WithInvalidWavHeader_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "test";
        var fileContent = new byte[] { 0x00, 0x01, 0x02, 0x03 }; // Invalid WAV

        // Act
        var result = SoundAggregate.Create(id, name, fileContent);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("SOUND_INVALID_WAV"));
        });
    }

    [Fact]
    public void Create_WithMissingRiffHeader_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "test";
        var fileContent = CreateInvalidWavFileNoRiff();

        // Act
        var result = SoundAggregate.Create(id, name, fileContent);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_INVALID_WAV_RIFF");
        });
    }

    [Fact]
    public void Create_WithMissingWaveHeader_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "test";
        var fileContent = CreateInvalidWavFileNoWave();

        // Act
        var result = SoundAggregate.Create(id, name, fileContent);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_INVALID_WAV_WAVE");
        });
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_WithValidName_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidSoundAggregate();
        var newName = "updated_name";

        // Act
        var result = aggregate.Update(name: newName);

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.Name.Should().Be(newName);
            aggregate.DomainEvents.Should().HaveCount(2); // Created + Updated
            aggregate.DomainEvents.Last().Should().BeOfType<SoundUpdatedEvent>();
        });
    }

    [Fact]
    public void Update_WithValidFileContent_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidSoundAggregate();
        var newFileContent = CreateValidWavFile(sampleRate: 8000);

        // Act
        var result = aggregate.Update(fileContent: newFileContent);

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.FileContent.Should().BeEquivalentTo(newFileContent);
            aggregate.SampleRate.Should().Be(8000);
            aggregate.DomainEvents.Should().HaveCount(2);
        });
    }

    [Fact]
    public void Update_WithDeletedAggregate_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidSoundAggregate();
        aggregate.Delete();

        // Act
        var result = aggregate.Update(name: "new_name");

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_DELETED");
        });
    }

    [Fact]
    public void Update_WithEmptyName_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidSoundAggregate();

        // Act
        var result = aggregate.Update(name: "");

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_NAME_REQUIRED");
        });
    }

    [Fact]
    public void Update_WithInvalidFileContent_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidSoundAggregate();

        // Act
        var result = aggregate.Update(fileContent: new byte[] { 0x00 });

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("SOUND_INVALID_WAV"));
        });
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_WithValidAggregate_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidSoundAggregate();

        // Act
        var result = aggregate.Delete();

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.IsDeleted.Should().BeTrue();
            aggregate.DomainEvents.Should().HaveCount(2);
            aggregate.DomainEvents.Last().Should().BeOfType<SoundDeletedEvent>();
        });
    }

    [Fact]
    public void Delete_WithAlreadyDeletedAggregate_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidSoundAggregate();
        aggregate.Delete();

        // Act
        var result = aggregate.Delete();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SOUND_ALREADY_DELETED");
        });
    }

    #endregion

    #region Event Sourcing Tests

    [Fact]
    public void Apply_SoundCreatedEvent_ShouldSetProperties()
    {
        // Arrange
        var aggregate = new SoundAggregate();
        var @event = new SoundCreatedEvent(
            Guid.NewGuid(),
            "test",
            CreateValidWavFile(),
            TimeSpan.FromSeconds(1),
            16000,
            1);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.Id.Should().Be(@event.Id);
        aggregate.Name.Should().Be(@event.Name);
        aggregate.FileContent.Should().BeEquivalentTo(@event.FileContent);
        aggregate.Duration.Should().Be(@event.Duration);
        aggregate.SampleRate.Should().Be(@event.SampleRate);
        aggregate.Channels.Should().Be(@event.Channels);
        aggregate.IsDeleted.Should().BeFalse();
        aggregate.CreatedAt.Should().Be(@event.OccurredOn);
        aggregate.UpdatedAt.Should().Be(@event.OccurredOn);
    }

    [Fact]
    public void Apply_SoundUpdatedEvent_WithName_ShouldUpdateName()
    {
        // Arrange
        var aggregate = CreateValidSoundAggregate();
        var @event = new SoundUpdatedEvent(aggregate.Id, name: "updated");

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.Name.Should().Be("updated");
        aggregate.UpdatedAt.Should().Be(@event.OccurredOn);
    }

    [Fact]
    public void Apply_SoundUpdatedEvent_WithFileContent_ShouldUpdateFileAndMetadata()
    {
        // Arrange
        var aggregate = CreateValidSoundAggregate();
        var newContent = CreateValidWavFile(sampleRate: 8000);
        var @event = new SoundUpdatedEvent(
            aggregate.Id,
            fileContent: newContent,
            duration: TimeSpan.FromSeconds(2),
            sampleRate: 8000,
            channels: 2);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.FileContent.Should().BeEquivalentTo(newContent);
        aggregate.Duration.Should().Be(TimeSpan.FromSeconds(2));
        aggregate.SampleRate.Should().Be(8000);
        aggregate.Channels.Should().Be(2);
    }

    [Fact]
    public void Apply_SoundDeletedEvent_ShouldMarkAsDeleted()
    {
        // Arrange
        var aggregate = CreateValidSoundAggregate();
        var @event = new SoundDeletedEvent(aggregate.Id);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Crée un fichier WAV valide pour les tests
    /// Format: RIFF WAV, 16 bits, mono ou stereo, sample rate configurable
    /// </summary>
    private static byte[] CreateValidWavFile(int sampleRate = 16000, int channels = 1, int durationMs = 100)
    {
        var numSamples = sampleRate * durationMs / 1000;
        var dataSize = numSamples * channels * 2; // 16 bits = 2 bytes per sample
        var fileSize = 36 + dataSize;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // RIFF header
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(fileSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // chunk size
        writer.Write((short)1); // PCM format
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2); // byte rate
        writer.Write((short)(channels * 2)); // block align
        writer.Write((short)16); // bits per sample

        // data chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        // Write silence (zeros)
        for (int i = 0; i < numSamples * channels; i++)
        {
            writer.Write((short)0);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Crée un fichier invalide sans header RIFF
    /// </summary>
    private static byte[] CreateInvalidWavFileNoRiff()
    {
        var buffer = new byte[44];
        System.Text.Encoding.ASCII.GetBytes("DATA").CopyTo(buffer, 0); // Invalid
        System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(buffer, 8);
        return buffer;
    }

    /// <summary>
    /// Crée un fichier invalide sans format WAVE
    /// </summary>
    private static byte[] CreateInvalidWavFileNoWave()
    {
        var buffer = new byte[44];
        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(buffer, 0);
        System.Text.Encoding.ASCII.GetBytes("DATA").CopyTo(buffer, 8); // Invalid
        return buffer;
    }

    /// <summary>
    /// Crée un SoundAggregate valide pour les tests
    /// </summary>
    private static SoundAggregate CreateValidSoundAggregate()
    {
        var result = SoundAggregate.Create(
            Guid.NewGuid(),
            "test_sound",
            CreateValidWavFile());

        return result.Match(
            Succ: aggregate => aggregate,
            Fail: _ => throw new Exception("Failed to create valid aggregate"));
    }

    #endregion
}
