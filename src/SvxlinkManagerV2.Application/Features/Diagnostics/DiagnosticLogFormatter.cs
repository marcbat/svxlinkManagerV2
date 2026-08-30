using System.Globalization;
using System.Text;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Application.Features.Diagnostics;

/// <summary>
/// Mise en forme texte des buffers de logs, partagée par l'export depuis les pages Logs
/// et par l'archive de diagnostic, afin que le fichier exporté reflète exactement ce que
/// l'utilisateur voit à l'écran.
/// </summary>
public static class DiagnosticLogFormatter
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>
    /// Applique le filtre texte de l'interface : recherche insensible à la casse dans le message.
    /// Un filtre vide retourne la totalité des entrées.
    /// </summary>
    /// <param name="logs">Entrées du buffer.</param>
    /// <param name="searchTerm">Terme de filtrage saisi à l'écran, éventuellement vide.</param>
    public static List<SvxLinkLogEntry> Filter(IEnumerable<SvxLinkLogEntry> logs, string? searchTerm)
        => string.IsNullOrEmpty(searchTerm)
            ? logs.ToList()
            : logs.Where(l => l.Message.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Met en forme les logs au format texte, précédés d'un en-tête rappelant la source,
    /// la date d'export et le filtre appliqué. Les secrets éventuellement présents dans les
    /// messages sont expurgés.
    /// </summary>
    /// <param name="sourceLabel">Libellé de la source (ex. « SVXLink »).</param>
    /// <param name="logs">Entrées déjà filtrées à exporter.</param>
    /// <param name="searchTerm">Filtre appliqué à l'écran, rappelé dans l'en-tête.</param>
    /// <param name="exportedAt">Horodatage de l'export.</param>
    public static string Format(
        string sourceLabel,
        IEnumerable<SvxLinkLogEntry> logs,
        string? searchTerm,
        DateTime exportedAt)
    {
        var entries = logs.ToList();
        var builder = new StringBuilder();

        builder.AppendLine($"# Logs {sourceLabel} — SvxLink Manager V2");
        builder.AppendLine($"# Export du {exportedAt.ToString("dd/MM/yyyy à HH:mm:ss", CultureInfo.GetCultureInfo("fr-FR"))}");
        builder.AppendLine(string.IsNullOrEmpty(searchTerm)
            ? "# Filtre appliqué : aucun"
            : $"# Filtre appliqué : \"{searchTerm}\"");
        builder.AppendLine($"# {entries.Count} ligne(s)");
        builder.AppendLine();

        foreach (var entry in entries)
            builder.AppendLine(FormatEntry(entry));

        return DiagnosticSecretRedactor.Redact(builder.ToString());
    }

    /// <summary>
    /// Construit le nom du fichier d'export : source et horodatage, comme demandé au support.
    /// </summary>
    /// <param name="sourceKey">Identifiant de la source en minuscules (ex. « svxlink »).</param>
    /// <param name="exportedAt">Horodatage de l'export.</param>
    public static string BuildFileName(string sourceKey, DateTime exportedAt)
        => $"logs-{sourceKey}-{exportedAt:yyyyMMdd-HHmmss}.txt";

    private static string FormatEntry(SvxLinkLogEntry entry)
        => $"{entry.Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture)} [{FormatLevel(entry.Level)}] {entry.Message}";

    private static string FormatLevel(SvxLinkLogLevel level) => level switch
    {
        SvxLinkLogLevel.Error => "ERREUR",
        SvxLinkLogLevel.Warning => "ALERTE",
        _ => "INFO  "
    };
}
