using FluentAssertions;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Tests.Common;

/// <summary>
/// Tests unitaires pour CtcssMapper
/// </summary>
public class CtcssMapperTests
{
    #region FrequencyToCode Tests

    [Fact]
    public void FrequencyToCode_WithNull_ShouldReturnZeroCode()
    {
        // Act
        var result = CtcssMapper.FrequencyToCode(null);

        // Assert
        result.Should().Be("0000");
    }

    [Theory]
    [InlineData(67.0, "0001")]
    [InlineData(71.9, "0002")]
    [InlineData(74.4, "0003")]
    [InlineData(77.0, "0004")]
    [InlineData(100.0, "0012")]
    [InlineData(136.5, "0021")]
    [InlineData(203.5, "0032")]
    [InlineData(250.3, "0038")]
    public void FrequencyToCode_WithValidFrequency_ShouldReturnCorrectCode(decimal frequency, string expectedCode)
    {
        // Act
        var result = CtcssMapper.FrequencyToCode(frequency);

        // Assert
        result.Should().Be(expectedCode);
    }

    [Fact]
    public void FrequencyToCode_WithUnknownFrequency_ShouldReturnZeroCode()
    {
        // Arrange - fréquence non présente dans la table
        var unknownFrequency = 99.9m;

        // Act
        var result = CtcssMapper.FrequencyToCode(unknownFrequency);

        // Assert
        result.Should().Be("0000");
    }

    #endregion

    #region CodeToFrequencyHz Tests

    [Fact]
    public void CodeToFrequencyHz_WithZeroCode_ShouldReturnNull()
    {
        // Act
        var result = CtcssMapper.CodeToFrequencyHz("0000");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("0001", 67.0)]
    [InlineData("0002", 71.9)]
    [InlineData("0012", 100.0)]
    [InlineData("0021", 136.5)]
    [InlineData("0038", 250.3)]
    public void CodeToFrequencyHz_WithValidCode_ShouldReturnCorrectFrequency(string code, decimal expectedFrequency)
    {
        // Act
        var result = CtcssMapper.CodeToFrequencyHz(code);

        // Assert
        result.Should().Be(expectedFrequency);
    }

    [Fact]
    public void CodeToFrequencyHz_WithNullCode_ShouldReturnNull()
    {
        // Act
        var result = CtcssMapper.CodeToFrequencyHz(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CodeToFrequencyHz_WithEmptyCode_ShouldReturnNull()
    {
        // Act
        var result = CtcssMapper.CodeToFrequencyHz("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CodeToFrequencyHz_WithNonNumericCode_ShouldReturnNull()
    {
        // Act
        var result = CtcssMapper.CodeToFrequencyHz("ABCD");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CodeToFrequencyHz_WithOutOfRangeCode_ShouldReturnNull()
    {
        // Arrange - code hors plage (> 0038)
        var outOfRangeCode = "0099";

        // Act
        var result = CtcssMapper.CodeToFrequencyHz(outOfRangeCode);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CodeToFrequencyHz_WithNegativeCode_ShouldReturnNull()
    {
        // Act
        var result = CtcssMapper.CodeToFrequencyHz("-001");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region IsValidFrequency Tests

    [Fact]
    public void IsValidFrequency_WithNull_ShouldReturnTrue()
    {
        // Act
        var result = CtcssMapper.IsValidFrequency(null);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(67.0)]
    [InlineData(71.9)]
    [InlineData(136.5)]
    [InlineData(250.3)]
    public void IsValidFrequency_WithValidFrequency_ShouldReturnTrue(decimal frequency)
    {
        // Act
        var result = CtcssMapper.IsValidFrequency(frequency);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(66.0)]
    [InlineData(99.9)]
    [InlineData(251.0)]
    public void IsValidFrequency_WithInvalidFrequency_ShouldReturnFalse(decimal frequency)
    {
        // Act
        var result = CtcssMapper.IsValidFrequency(frequency);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetAllFrequencies Tests

    [Fact]
    public void GetAllFrequencies_ShouldReturnAllValidFrequencies()
    {
        // Act
        var frequencies = CtcssMapper.GetAllFrequencies();

        // Assert
        frequencies.Should().NotBeEmpty();
        frequencies.Should().Contain(67.0m);
        frequencies.Should().Contain(136.5m);
        frequencies.Should().Contain(250.3m);
        // Vérifier qu'il y a 38 fréquences valides
        frequencies.Should().HaveCount(38);
    }

    #endregion

    #region Round-trip Tests

    [Theory]
    [InlineData(67.0)]
    [InlineData(71.9)]
    [InlineData(136.5)]
    [InlineData(250.3)]
    public void FrequencyToCode_ThenCodeToFrequency_ShouldRoundTrip(decimal frequency)
    {
        // Act
        var code = CtcssMapper.FrequencyToCode(frequency);
        var result = CtcssMapper.CodeToFrequencyHz(code);

        // Assert
        result.Should().Be(frequency);
    }

    #endregion
}
