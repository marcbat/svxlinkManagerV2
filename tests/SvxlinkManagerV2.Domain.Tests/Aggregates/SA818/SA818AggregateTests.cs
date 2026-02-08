using FluentAssertions;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.SA818.Events;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.SA818;

/// <summary>
/// Tests unitaires pour SA818Aggregate
/// </summary>
public class SA818AggregateTests
{
    #region Factory Create Tests

    [Fact]
    public void Create_WithDefaultParameters_ShouldSucceed()
    {
        // Act
        var result = SA818Aggregate.Create();

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.Id.Should().Be(SA818Aggregate.FixedId);
            aggregate.Volume.Should().Be(4);
            aggregate.Squelch.Should().Be(4);
            aggregate.Bandwidth.Should().Be(SA818Bandwidth.Wide25kHz);
            aggregate.PreEmph.Should().BeFalse();
            aggregate.HighPass.Should().BeFalse();
            aggregate.LowPass.Should().BeFalse();
            aggregate.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<SA818ConfigurationUpdatedEvent>();
        });
    }

    [Fact]
    public void Create_WithCustomParameters_ShouldSucceed()
    {
        // Arrange
        const int volume = 6;
        const int squelch = 5;
        const SA818Bandwidth bandwidth = SA818Bandwidth.Narrow12_5kHz;
        const bool preEmph = true;
        const bool highPass = true;
        const bool lowPass = false;

        // Act
        var result = SA818Aggregate.Create(volume, squelch, bandwidth, preEmph, highPass, lowPass);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.Id.Should().Be(SA818Aggregate.FixedId);
            aggregate.Volume.Should().Be(volume);
            aggregate.Squelch.Should().Be(squelch);
            aggregate.Bandwidth.Should().Be(bandwidth);
            aggregate.PreEmph.Should().Be(preEmph);
            aggregate.HighPass.Should().Be(highPass);
            aggregate.LowPass.Should().Be(lowPass);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Create_WithInvalidVolume_ShouldFail(int invalidVolume)
    {
        // Act
        var result = SA818Aggregate.Create(volume: invalidVolume);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SA818_VOLUME_INVALID");
        });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(-10)]
    [InlineData(100)]
    public void Create_WithInvalidSquelch_ShouldFail(int invalidSquelch)
    {
        // Act
        var result = SA818Aggregate.Create(squelch: invalidSquelch);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SA818_SQUELCH_INVALID");
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Create_WithValidVolume_ShouldSucceed(int validVolume)
    {
        // Act
        var result = SA818Aggregate.Create(volume: validVolume);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.Volume.Should().Be(validVolume);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Create_WithValidSquelch_ShouldSucceed(int validSquelch)
    {
        // Act
        var result = SA818Aggregate.Create(squelch: validSquelch);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.Squelch.Should().Be(validSquelch);
        });
    }

    #endregion

    #region UpdateConfiguration Tests

    [Fact]
    public void UpdateConfiguration_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var aggregate = SA818Aggregate.Create().IfFail(() => throw new Exception("Setup failed"));
        const int newVolume = 7;
        const int newSquelch = 6;
        const SA818Bandwidth newBandwidth = SA818Bandwidth.Narrow12_5kHz;
        const bool newPreEmph = true;
        const bool newHighPass = true;
        const bool newLowPass = true;

        // Act
        var result = aggregate.UpdateConfiguration(newVolume, newSquelch, newBandwidth, newPreEmph, newHighPass, newLowPass);

        // Assert
        result.ShouldBeSuccess();
        aggregate.Volume.Should().Be(newVolume);
        aggregate.Squelch.Should().Be(newSquelch);
        aggregate.Bandwidth.Should().Be(newBandwidth);
        aggregate.PreEmph.Should().Be(newPreEmph);
        aggregate.HighPass.Should().Be(newHighPass);
        aggregate.LowPass.Should().Be(newLowPass);
        aggregate.DomainEvents.Should().HaveCount(2); // Create + Update
        aggregate.DomainEvents.Last().Should().BeOfType<SA818ConfigurationUpdatedEvent>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    public void UpdateConfiguration_WithInvalidVolume_ShouldFail(int invalidVolume)
    {
        // Arrange
        var aggregate = SA818Aggregate.Create().IfFail(() => throw new Exception("Setup failed"));

        // Act
        var result = aggregate.UpdateConfiguration(invalidVolume, 4, SA818Bandwidth.Wide25kHz, false, false, false);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SA818_VOLUME_INVALID");
        });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void UpdateConfiguration_WithInvalidSquelch_ShouldFail(int invalidSquelch)
    {
        // Arrange
        var aggregate = SA818Aggregate.Create().IfFail(() => throw new Exception("Setup failed"));

        // Act
        var result = aggregate.UpdateConfiguration(4, invalidSquelch, SA818Bandwidth.Wide25kHz, false, false, false);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SA818_SQUELCH_INVALID");
        });
    }

    #endregion

    #region Event Sourcing Tests

    [Fact]
    public void Apply_SA818ConfigurationUpdatedEvent_ShouldUpdateProperties()
    {
        // Arrange
        var aggregate = new SA818Aggregate();
        var eventData = new SA818ConfigurationUpdatedEvent(
            SA818Aggregate.FixedId,
            volume: 7,
            squelch: 5,
            bandwidth: SA818Bandwidth.Narrow12_5kHz,
            preEmph: true,
            highPass: true,
            lowPass: false);

        // Act
        aggregate.Apply(eventData);

        // Assert
        aggregate.Id.Should().Be(SA818Aggregate.FixedId);
        aggregate.Volume.Should().Be(7);
        aggregate.Squelch.Should().Be(5);
        aggregate.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);
        aggregate.PreEmph.Should().BeTrue();
        aggregate.HighPass.Should().BeTrue();
        aggregate.LowPass.Should().BeFalse();
    }

    [Fact]
    public void FixedId_ShouldBeConstant()
    {
        // Assert
        SA818Aggregate.FixedId.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    }

    #endregion

    #region Bandwidth Enum Tests

    [Fact]
    public void SA818Bandwidth_Narrow12_5kHz_ShouldBeZero()
    {
        // Assert
        ((int)SA818Bandwidth.Narrow12_5kHz).Should().Be(0);
    }

    [Fact]
    public void SA818Bandwidth_Wide25kHz_ShouldBeOne()
    {
        // Assert
        ((int)SA818Bandwidth.Wide25kHz).Should().Be(1);
    }

    #endregion
}
