using FluentAssertions;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Infrastructure.Hardware;

namespace SvxlinkManagerV2.Infrastructure.Tests.Hardware;

/// <summary>
/// Tests unitaires pour le CtcssMapper.
/// Valide le mapping des 38 fréquences CTCSS standard, 
/// la gestion des cas null/0, et la validation des valeurs invalides.
/// </summary>
public class CtcssMapperTests
{
    #region Tests des valeurs standard

    [Theory]
    [InlineData(67.0, "0001")]
    [InlineData(71.9, "0002")]
    [InlineData(74.4, "0003")]
    [InlineData(77.0, "0004")]
    [InlineData(79.7, "0005")]
    [InlineData(82.5, "0006")]
    [InlineData(85.4, "0007")]
    [InlineData(88.5, "0008")]
    [InlineData(91.5, "0009")]
    [InlineData(94.8, "0010")]
    [InlineData(97.4, "0011")]
    [InlineData(100.0, "0012")]
    [InlineData(103.5, "0013")]
    [InlineData(107.2, "0014")]
    [InlineData(110.9, "0015")]
    [InlineData(114.8, "0016")]
    [InlineData(118.8, "0017")]
    [InlineData(123.0, "0018")]
    [InlineData(127.3, "0019")]
    [InlineData(131.8, "0020")]
    [InlineData(136.5, "0021")]
    [InlineData(141.3, "0022")]
    [InlineData(146.2, "0023")]
    [InlineData(151.4, "0024")]
    [InlineData(156.7, "0025")]
    [InlineData(162.2, "0026")]
    [InlineData(167.9, "0027")]
    [InlineData(173.8, "0028")]
    [InlineData(179.9, "0029")]
    [InlineData(186.2, "0030")]
    [InlineData(192.8, "0031")]
    [InlineData(203.5, "0032")]
    [InlineData(210.7, "0033")]
    [InlineData(218.1, "0034")]
    [InlineData(225.7, "0035")]
    [InlineData(233.6, "0036")]
    [InlineData(241.8, "0037")]
    [InlineData(250.3, "0038")]
    public void ToSA818Code_WithStandardCtcssFrequency_ShouldReturnCorrectCode(double ctcssHz, string expectedCode)
    {
        // Arrange
        var ctcssDecimal = (decimal)ctcssHz;

        // Act
        var result = CtcssMapper.ToSA818Code(ctcssDecimal);

        // Assert
        result.ShouldBeSuccess(code => code.Should().Be(expectedCode));
    }

    #endregion

    #region Tests des cas null et 0

    [Fact]
    public void ToSA818Code_WithNull_ShouldReturnCode0000()
    {
        // Act
        var result = CtcssMapper.ToSA818Code(null);

        // Assert
        result.ShouldBeSuccess(code => code.Should().Be("0000"));
    }

    [Fact]
    public void ToSA818Code_WithZero_ShouldReturnCode0000()
    {
        // Act
        var result = CtcssMapper.ToSA818Code(0m);

        // Assert
        result.ShouldBeSuccess(code => code.Should().Be("0000"));
    }

    #endregion

    #region Tests des valeurs invalides

    [Theory]
    [InlineData(50.0)]   // Fréquence trop basse
    [InlineData(66.9)]   // Juste en dessous de la première valeur
    [InlineData(68.5)]   // Entre deux valeurs
    [InlineData(135.0)]  // Entre deux valeurs
    [InlineData(250.4)]  // Juste au-dessus de la dernière valeur
    [InlineData(300.0)]  // Fréquence trop haute
    [InlineData(999.9)]  // Valeur complètement invalide
    public void ToSA818Code_WithInvalidFrequency_ShouldReturnError(double invalidFrequency)
    {
        // Arrange
        var ctcssDecimal = (decimal)invalidFrequency;

        // Act
        var result = CtcssMapper.ToSA818Code(ctcssDecimal);

        // Assert
        result.ShouldBeFail(errors =>
        {
            var error = errors.Head;
            error.Message.Should().Contain("Fréquence CTCSS invalide");
            error.Message.Should().Contain($"{ctcssDecimal} Hz");
        });
    }

    #endregion

    #region Tests de validation du message d'erreur

    [Fact]
    public void ToSA818Code_WithInvalidFrequency_ShouldReturnDescriptiveError()
    {
        // Arrange
        var invalidFrequency = 999.9m;

        // Act
        var result = CtcssMapper.ToSA818Code(invalidFrequency);

        // Assert
        result.ShouldBeFail(errors =>
        {
            var error = errors.Head;
            error.Message.Should().Contain("Fréquence CTCSS invalide");
            error.Message.Should().MatchRegex(@"999[.,]9 Hz"); // Accepte virgule ou point
            error.Message.Should().Contain("67.0 Hz");
            error.Message.Should().Contain("250.3 Hz");
            error.Message.Should().Contain("38 valeurs CTCSS standard");
        });
    }

    #endregion

    #region Tests de validation du format des codes

    [Fact]
    public void ToSA818Code_AllStandardCodes_ShouldHave4DigitFormat()
    {
        // Arrange
        var allStandardFrequencies = new[]
        {
            67.0m, 71.9m, 74.4m, 77.0m, 79.7m, 82.5m, 85.4m, 88.5m, 91.5m, 94.8m,
            97.4m, 100.0m, 103.5m, 107.2m, 110.9m, 114.8m, 118.8m, 123.0m, 127.3m, 131.8m,
            136.5m, 141.3m, 146.2m, 151.4m, 156.7m, 162.2m, 167.9m, 173.8m, 179.9m, 186.2m,
            192.8m, 203.5m, 210.7m, 218.1m, 225.7m, 233.6m, 241.8m, 250.3m
        };

        // Act & Assert
        foreach (var frequency in allStandardFrequencies)
        {
            var result = CtcssMapper.ToSA818Code(frequency);
            result.ShouldBeSuccess(code =>
            {
                code.Should().HaveLength(4, $"code for {frequency} Hz should have 4 digits");
                code.Should().MatchRegex(@"^\d{4}$", "code should contain only digits");
            });
        }
    }

    #endregion
}
