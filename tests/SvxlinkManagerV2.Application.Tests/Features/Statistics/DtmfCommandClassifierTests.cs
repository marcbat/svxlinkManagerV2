using FluentAssertions;
using SvxlinkManagerV2.Application.Features.Statistics;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Application.Tests.Features.Statistics;

/// <summary>
/// Tests du classement des codes DTMF reçus.
/// </summary>
public class DtmfCommandClassifierTests
{
    private static readonly Dictionary<int, string> Salons = new() { [208] = "TG208", [500] = "Perroquet" };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(19)]
    public void Classify_ShouldRecogniseSvxLinkModules(int code)
    {
        var (category, _) = DtmfCommandClassifier.Classify(code.ToString(), Salons);

        category.Should().Be(DtmfCommandCategory.SvxLinkModule);
    }

    [Fact]
    public void Classify_ShouldRecogniseSystemCommandsAndUseTheirDescription()
    {
        var (category, label) = DtmfCommandClassifier.Classify(
            DtmfSystemCommands.DefaultSalon.ToString(), Salons);

        category.Should().Be(DtmfCommandCategory.SystemCommand);
        label.Should().Be("Revenir au salon par défaut");
    }

    [Fact]
    public void Classify_ShouldTreatTheRestOfTheReservedRangeAsAnnouncements()
    {
        // 301 est une annonce vocale, pas une commande système.
        var (category, _) = DtmfCommandClassifier.Classify("301", Salons);

        category.Should().Be(DtmfCommandCategory.Announcement);
    }

    [Fact]
    public void Classify_ShouldNameTheSalonBehindTheCode()
    {
        var (category, label) = DtmfCommandClassifier.Classify("208", Salons);

        category.Should().Be(DtmfCommandCategory.SalonSwitch);
        label.Should().Be("Salon TG208");
    }

    [Fact]
    public void Classify_ShouldFlagACodeWithoutSalon()
    {
        var (category, _) = DtmfCommandClassifier.Classify("4242", Salons);

        category.Should().Be(DtmfCommandCategory.Unknown);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("12#")]
    [InlineData("")]
    public void Classify_ShouldFlagNonNumericCodes(string raw)
    {
        var (category, label) = DtmfCommandClassifier.Classify(raw, Salons);

        category.Should().Be(DtmfCommandCategory.Unknown);
        label.Should().Be("Code non numérique");
    }

    [Fact]
    public void Classify_ShouldTolerateSurroundingWhitespace()
    {
        var (category, _) = DtmfCommandClassifier.Classify("  208  ", Salons);

        category.Should().Be(DtmfCommandCategory.SalonSwitch);
    }
}
