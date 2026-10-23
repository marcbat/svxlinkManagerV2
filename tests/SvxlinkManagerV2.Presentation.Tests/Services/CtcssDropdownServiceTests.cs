using System.Globalization;
using FluentAssertions;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Presentation.Services;
using Xunit;

namespace SvxlinkManagerV2.Presentation.Tests.Services;

/// <summary>
/// Tests de l'aller-retour entre une tonalité CTCSS et la valeur de son option.
///
/// Le cas qui compte est la culture à virgule décimale : l'assistant écrivait « 67,0 »
/// et le relisait en culture invariante, où la virgule sépare les milliers — la tonalité
/// devenait 670 Hz et l'installation ne pouvait plus se terminer (#166).
/// </summary>
public class CtcssDropdownServiceTests
{
    /// <summary>Cultures représentatives des postes visés : Suisse, France, États-Unis.</summary>
    public static TheoryData<string> Cultures => new() { "fr-CH", "de-CH", "fr-FR", "en-US", "" };

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ToOptionValue_ShouldAlwaysUseADecimalPoint(string culture)
    {
        using var _ = new CultureScope(culture);

        CtcssDropdownService.ToOptionValue(67.0m).Should().Be("67.0");
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void ParseOptionValue_ShouldReturnTheFrequencyWrittenByToOptionValue(string culture)
    {
        using var _ = new CultureScope(culture);

        foreach (var frequency in CtcssMapper.GetAllFrequencies())
        {
            var value = CtcssDropdownService.ToOptionValue(frequency);

            CtcssDropdownService.ParseOptionValue(value).Should().Be(frequency);
        }
    }

    [Fact]
    public void ToOptionValue_ShouldBeEmptyWhenNoTone()
    {
        CtcssDropdownService.ToOptionValue(null).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseOptionValue_ShouldReturnNullWhenNoTone(string? value)
    {
        CtcssDropdownService.ParseOptionValue(value).Should().BeNull();
    }

    [Theory]
    [InlineData("67,0")]
    [InlineData("abc")]
    public void ParseOptionValue_ShouldRejectAValueNotWrittenByToOptionValue(string value)
    {
        // « 67,0 » ne doit plus jamais devenir 670 : mieux vaut aucune tonalité qu'une fausse.
        CtcssDropdownService.ParseOptionValue(value).Should().BeNull();
    }

    /// <summary>Applique une culture le temps d'un test, puis rétablit celle du thread.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string culture)
            => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}
