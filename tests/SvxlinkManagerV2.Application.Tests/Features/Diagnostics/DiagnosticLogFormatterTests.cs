using FluentAssertions;
using SvxlinkManagerV2.Application.Features.Diagnostics;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Application.Tests.Features.Diagnostics;

/// <summary>
/// Tests unitaires de la mise en forme texte des logs exportés depuis les pages Logs.
/// </summary>
public class DiagnosticLogFormatterTests
{
    private static readonly DateTime ExportedAt = new(2026, 8, 30, 14, 32, 11);

    private static readonly List<SvxLinkLogEntry> Logs =
    [
        new(new DateTime(2026, 8, 30, 14, 30, 0, 100), "Connexion au reflector établie", SvxLinkLogLevel.Info),
        new(new DateTime(2026, 8, 30, 14, 30, 1, 200), "Squelch ouvert", SvxLinkLogLevel.Info),
        new(new DateTime(2026, 8, 30, 14, 30, 2, 300), "Connexion perdue", SvxLinkLogLevel.Error)
    ];

    [Fact]
    public void Filter_ShouldReturnEveryEntry_WhenSearchTermIsEmpty()
    {
        DiagnosticLogFormatter.Filter(Logs, null).Should().HaveCount(3);
        DiagnosticLogFormatter.Filter(Logs, string.Empty).Should().HaveCount(3);
    }

    [Fact]
    public void Filter_ShouldMatchMessagesIgnoringCase()
    {
        var result = DiagnosticLogFormatter.Filter(Logs, "CONNEXION");

        result.Should().HaveCount(2);
        result.Should().OnlyContain(l => l.Message.Contains("Connexion"));
    }

    [Fact]
    public void Format_ShouldExportOnlyTheFilteredEntries()
    {
        var filtered = DiagnosticLogFormatter.Filter(Logs, "perdue");

        var result = DiagnosticLogFormatter.Format("SVXLink", filtered, "perdue", ExportedAt);

        result.Should().Contain("Connexion perdue");
        result.Should().NotContain("Squelch ouvert");
        result.Should().Contain("# Filtre appliqué : \"perdue\"");
        result.Should().Contain("# 1 ligne(s)");
    }

    [Fact]
    public void Format_ShouldWriteHeaderSourceAndTimestampedEntries()
    {
        var result = DiagnosticLogFormatter.Format("SVXLink", Logs, searchTerm: null, ExportedAt);

        result.Should().StartWith("# Logs SVXLink — SvxLink Manager V2");
        result.Should().Contain("# Export du 30/08/2026 à 14:32:11");
        result.Should().Contain("# Filtre appliqué : aucun");
        result.Should().Contain("2026-08-30 14:30:00.100 [INFO  ] Connexion au reflector établie");
        result.Should().Contain("2026-08-30 14:30:02.300 [ERREUR] Connexion perdue");
    }

    [Fact]
    public void Format_ShouldRedactSecretsFoundInLogMessages()
    {
        List<SvxLinkLogEntry> logs =
        [
            new(ExportedAt, "ReflectorLogic AUTH_KEY=Magnifique123456789!", SvxLinkLogLevel.Info)
        ];

        var result = DiagnosticLogFormatter.Format("SVXLink", logs, searchTerm: null, ExportedAt);

        result.Should().NotContain("Magnifique123456789!");
        result.Should().Contain($"AUTH_KEY={DiagnosticSecretRedactor.RedactedValue}");
    }

    [Theory]
    [InlineData("svxlink", "logs-svxlink-20260830-143211.txt")]
    [InlineData("reflector", "logs-reflector-20260830-143211.txt")]
    public void BuildFileName_ShouldCarrySourceAndTimestamp(string sourceKey, string expected)
        => DiagnosticLogFormatter.BuildFileName(sourceKey, ExportedAt).Should().Be(expected);
}
