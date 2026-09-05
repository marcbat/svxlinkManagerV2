using System.Globalization;
using FluentAssertions;
using SvxlinkManagerV2.Presentation.Services;
using Xunit;

namespace SvxlinkManagerV2.Presentation.Tests.Services;

/// <summary>
/// Tests de la mise en forme des valeurs CSS.
///
/// Le cas qui compte est la culture à virgule décimale : le navigateur rejette
/// « width:74,0% » sans rien signaler, et la barre reste invisible.
/// </summary>
public class CssValueTests
{
    /// <summary>Cultures représentatives des postes visés : Suisse, France, États-Unis.</summary>
    public static TheoryData<string> Cultures => new() { "de-CH", "fr-FR", "en-US", "" };

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Percent_ShouldAlwaysUseADecimalPoint(string culture)
    {
        using var _ = new CultureScope(culture);

        CssValue.Percent(74.06).Should().Be("74.1%");
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Rgba_ShouldAlwaysUseADecimalPoint(string culture)
    {
        using var _ = new CultureScope(culture);

        CssValue.Rgba(108, 92, 231, 0.66).Should().Be("rgba(108, 92, 231, 0.66)");
    }

    [Theory]
    [InlineData(-20, "0.0%")]
    [InlineData(0, "0.0%")]
    [InlineData(100, "100.0%")]
    [InlineData(180, "100.0%")]
    public void Percent_ShouldStayWithinBounds(double percent, string expected)
    {
        CssValue.Percent(percent).Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, "rgba(0, 0, 0, 0.00)")]
    [InlineData(2, "rgba(0, 0, 0, 1.00)")]
    public void Rgba_ShouldClampOpacity(double alpha, string expected)
    {
        CssValue.Rgba(0, 0, 0, alpha).Should().Be(expected);
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
