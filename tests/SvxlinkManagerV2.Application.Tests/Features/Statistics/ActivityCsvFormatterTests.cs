using FluentAssertions;
using SvxlinkManagerV2.Application.Features.Statistics;
using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Application.Tests.Features.Statistics;

/// <summary>
/// Tests de l'export CSV de la chronologie.
/// </summary>
public class ActivityCsvFormatterTests
{
    private static readonly DateTimeOffset Moment = new(2026, 8, 30, 14, 5, 9, TimeSpan.FromHours(2));

    [Fact]
    public void Format_ShouldStartWithTheHeaderRow()
    {
        var csv = ActivityCsvFormatter.Format([]);

        csv.Should().StartWith("Date;Type;Événement;Salon;Durée (s)");
    }

    [Fact]
    public void Format_ShouldRenderAnEntry()
    {
        var csv = ActivityCsvFormatter.Format([
            new TimelineEntryDto(Moment, ActivityEventType.TalkerHeard, "Passage de HB9AAA", "TG208", TimeSpan.FromSeconds(12))
        ]);

        csv.Should().Contain("2026-08-30 14:05:09;Passage entendu;Passage de HB9AAA;TG208;12");
    }

    [Fact]
    public void Format_ShouldLeaveTheDurationEmptyWhenThereIsNone()
    {
        var csv = ActivityCsvFormatter.Format([
            new TimelineEntryDto(Moment, ActivityEventType.DtmfCommand, "Code DTMF 310", null, null)
        ]);

        csv.Should().Contain("Code DTMF 310;;");
    }

    [Fact]
    public void Format_ShouldQuoteAFieldContainingTheSeparator()
    {
        var csv = ActivityCsvFormatter.Format([
            new TimelineEntryDto(Moment, ActivityEventType.ReflectorLinkLost, "Perdue ; cause inconnue", null, null)
        ]);

        csv.Should().Contain("\"Perdue ; cause inconnue\"");
    }

    [Fact]
    public void Format_ShouldDoubleEmbeddedQuotes()
    {
        var csv = ActivityCsvFormatter.Format([
            new TimelineEntryDto(Moment, ActivityEventType.DtmfCommand, "Code \"310\"", null, null)
        ]);

        csv.Should().Contain("\"Code \"\"310\"\"\"");
    }

    [Fact]
    public void BuildFileName_ShouldCarryTheExportTimestamp()
    {
        var name = ActivityCsvFormatter.BuildFileName(new DateTime(2026, 8, 30, 14, 5, 9));

        name.Should().Be("statistiques-20260830-140509.csv");
    }

    [Fact]
    public void ToLabel_ShouldCoverEveryEventType()
    {
        foreach (var type in Enum.GetValues<ActivityEventType>())
            ActivityCsvFormatter.ToLabel(type).Should().NotBe(type.ToString(),
                $"la nature {type} doit avoir un libellé français");
    }
}
