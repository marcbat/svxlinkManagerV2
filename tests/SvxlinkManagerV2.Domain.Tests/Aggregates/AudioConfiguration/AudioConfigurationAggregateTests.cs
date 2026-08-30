using FluentAssertions;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration.Events;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.AudioConfiguration;

/// <summary>
/// Tests unitaires de l'aggregate des niveaux ALSA mémorisés.
/// </summary>
public class AudioConfigurationAggregateTests
{
    [Fact]
    public void Create_ShouldUseFixedId_AndKeepBothControls()
    {
        var result = AudioConfigurationAggregate.Create("ADC Gain", 3, "Line Out", 22);

        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.Id.Should().Be(AudioConfigurationAggregate.FixedId);
            aggregate.CaptureControl.Should().Be("ADC Gain");
            aggregate.CaptureLevel.Should().Be(3);
            aggregate.PlaybackControl.Should().Be("Line Out");
            aggregate.PlaybackLevel.Should().Be(22);
        });
    }

    [Fact]
    public void Create_ShouldEmitCreatedEvent()
    {
        var result = AudioConfigurationAggregate.Create("ADC Gain", 3, "Line Out", 22);

        result.ShouldBeSuccess(aggregate =>
            aggregate.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<AudioConfigurationCreated>());
    }

    [Fact]
    public void Create_ShouldTrimControlNames()
    {
        var result = AudioConfigurationAggregate.Create("  ADC Gain  ", 3, " Line Out ", 22);

        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.CaptureControl.Should().Be("ADC Gain");
            aggregate.PlaybackControl.Should().Be("Line Out");
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldFail_WhenCaptureControlIsBlank(string control)
    {
        var result = AudioConfigurationAggregate.Create(control, 3, "Line Out", 22);

        result.ShouldBeFail(errors =>
            errors.Should().Contain(error => error.Code == "AUDIO_CAPTURE_CONTROL_REQUIRED"));
    }

    [Fact]
    public void Create_ShouldFail_WhenPlaybackControlIsBlank()
    {
        var result = AudioConfigurationAggregate.Create("ADC Gain", 3, "", 22);

        result.ShouldBeFail(errors =>
            errors.Should().Contain(error => error.Code == "AUDIO_PLAYBACK_CONTROL_REQUIRED"));
    }

    [Fact]
    public void Create_ShouldFail_WhenLevelIsNegative()
    {
        var result = AudioConfigurationAggregate.Create("ADC Gain", -1, "Line Out", -2);

        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(error => error.Code == "AUDIO_CAPTURE_LEVEL_INVALID");
            errors.Should().Contain(error => error.Code == "AUDIO_PLAYBACK_LEVEL_INVALID");
        });
    }

    [Fact]
    public void Create_ShouldAcceptZeroLevels()
    {
        // Couper complètement un niveau est un réglage légitime, pas une erreur de saisie.
        var result = AudioConfigurationAggregate.Create("ADC Gain", 0, "Line Out", 0);

        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.CaptureLevel.Should().Be(0);
            aggregate.PlaybackLevel.Should().Be(0);
        });
    }

    [Fact]
    public void Create_ShouldNotBoundLevelsToAnyMaximum()
    {
        // La borne haute appartient à la carte son : le domaine ne peut pas la connaître.
        var result = AudioConfigurationAggregate.Create("Master", 1000, "Line Out", 65536);

        result.ShouldBeSuccess(aggregate => aggregate.PlaybackLevel.Should().Be(65536));
    }

    [Fact]
    public void UpdateLevels_ShouldReplaceStateAndEmitEvent()
    {
        var aggregate = CreateAggregate();
        aggregate.ClearDomainEvents();

        var result = aggregate.UpdateLevels("ADC Gain", 7, "Line Out", 18);

        result.ShouldBeSuccess();
        aggregate.CaptureLevel.Should().Be(7);
        aggregate.PlaybackLevel.Should().Be(18);
        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AudioLevelsUpdated>();
    }

    [Fact]
    public void UpdateLevels_ShouldFail_AndLeaveStateUntouched_WhenLevelIsNegative()
    {
        var aggregate = CreateAggregate();
        aggregate.ClearDomainEvents();

        var result = aggregate.UpdateLevels("ADC Gain", -5, "Line Out", 18);

        result.ShouldBeFail();
        aggregate.CaptureLevel.Should().Be(3);
        aggregate.PlaybackLevel.Should().Be(22);
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Targets_ShouldBeTrue_ForTheSameControls_IgnoringCaseAndSpacing()
    {
        var aggregate = CreateAggregate();

        aggregate.Targets("adc gain", " LINE OUT ").Should().BeTrue();
    }

    [Fact]
    public void Targets_ShouldBeFalse_WhenAControlDiffers()
    {
        var aggregate = CreateAggregate();

        aggregate.Targets("Mic1 Boost", "Line Out").Should().BeFalse();
    }

    [Fact]
    public void Targets_ShouldBeFalse_WhenAControlIsNull()
    {
        var aggregate = CreateAggregate();

        aggregate.Targets(null, "Line Out").Should().BeFalse();
    }

    private static AudioConfigurationAggregate CreateAggregate() =>
        AudioConfigurationAggregate.Create("ADC Gain", 3, "Line Out", 22)
            .Match(
                Succ: aggregate => aggregate,
                Fail: _ => throw new InvalidOperationException("Création attendue en succès."));
}
