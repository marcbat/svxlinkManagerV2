using System.Globalization;
using System.Text;
using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Application.Features.Statistics;

/// <summary>
/// Mise en forme CSV de la chronologie d'activité, sur le modèle de
/// <see cref="Diagnostics.DiagnosticLogFormatter"/> : le fichier exporté reflète exactement
/// ce que l'opérateur voit à l'écran, filtre compris.
/// </summary>
public static class ActivityCsvFormatter
{
    /// <summary>
    /// Séparateur de colonnes. Le point-virgule est ce qu'attend un tableur configuré en
    /// français ; la virgule y ferait atterrir toute la ligne dans une seule colonne.
    /// </summary>
    private const char Separator = ';';

    private const string Quote = "\"";

    /// <summary>Construit le contenu CSV, en-tête compris.</summary>
    /// <param name="entries">Lignes de chronologie déjà filtrées.</param>
    public static string Format(IEnumerable<TimelineEntryDto> entries)
    {
        var builder = new StringBuilder();

        builder.AppendLine(string.Join(Separator, "Date", "Type", "Événement", "Salon", "Durée (s)"));

        foreach (var entry in entries)
        {
            builder.AppendLine(string.Join(
                Separator,
                entry.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Escape(ToLabel(entry.Type)),
                Escape(entry.Label),
                Escape(entry.SalonName ?? string.Empty),
                entry.Duration is { } duration
                    ? Math.Round(duration.TotalSeconds).ToString("F0", CultureInfo.InvariantCulture)
                    : string.Empty));
        }

        return builder.ToString();
    }

    /// <summary>Construit le nom du fichier d'export.</summary>
    /// <param name="exportedAt">Horodatage de l'export.</param>
    public static string BuildFileName(DateTime exportedAt)
        => $"statistiques-{exportedAt:yyyyMMdd-HHmmss}.csv";

    /// <summary>Libellé français d'une nature d'événement, partagé par l'écran et l'export.</summary>
    /// <param name="type">Nature de l'événement.</param>
    public static string ToLabel(ActivityEventType type) => type switch
    {
        ActivityEventType.TalkerHeard => "Passage entendu",
        ActivityEventType.LocalTransmission => "Réception locale",
        ActivityEventType.DtmfCommand => "Commande DTMF",
        ActivityEventType.ReflectorLinkUp => "Fin de liaison",
        ActivityEventType.ReflectorLinkLost => "Liaison perdue",
        ActivityEventType.ReflectorLinkFailed => "Liaison impossible",
        ActivityEventType.ReflectorOutage => "Liaison rétablie",
        ActivityEventType.RxDistortion => "Écrêtage",
        ActivityEventType.ApplicationStarted => "Démarrage",
        ActivityEventType.ApplicationStopped => "Arrêt",
        _ => type.ToString()
    };

    /// <summary>
    /// Protège un champ contenant le séparateur, un guillemet ou un saut de ligne,
    /// selon la convention CSV : encadrement par des guillemets, doublés à l'intérieur.
    /// </summary>
    private static string Escape(string value)
    {
        var needsQuoting =
            value.IndexOf(Separator) >= 0 ||
            value.Contains(Quote, StringComparison.Ordinal) ||
            value.Contains('\n') ||
            value.Contains('\r');

        return needsQuoting
            ? Quote + value.Replace(Quote, Quote + Quote, StringComparison.Ordinal) + Quote
            : value;
    }
}
