using System.Globalization;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

/// <summary>
/// Helpers de mise en forme des valeurs numériques pour les annonces vocales françaises.
/// Le moteur TTS lit correctement les chiffres : seules les unités et les accords
/// doivent être explicités.
/// </summary>
internal static class InfoTextFormatter
{
    /// <summary>
    /// Met un mot au pluriel selon la valeur qui le précède (règle française : 0 et 1 restent au singulier).
    /// </summary>
    internal static string Plural(double value, string singular, string plural)
        => Math.Abs(value) < 2 ? singular : plural;

    /// <summary>
    /// Formate un entier arrondi sans séparateur de milliers, lisible par le moteur TTS.
    /// </summary>
    internal static string Round(double value)
        => Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Convertit un nombre d'octets en une quantité annonçable (mégaoctets ou gigaoctets).
    /// </summary>
    internal static string Bytes(long bytes)
    {
        const double megabyte = 1024d * 1024d;
        const double gigabyte = megabyte * 1024d;

        if (bytes >= gigabyte)
        {
            var gigabytes = Math.Round(bytes / gigabyte, 1);
            var text = gigabytes.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');
            return $"{text} {Plural(gigabytes, "gigaoctet", "gigaoctets")}";
        }

        var megabytes = Math.Round(bytes / megabyte);
        return $"{Round(megabytes)} {Plural(megabytes, "mégaoctet", "mégaoctets")}";
    }

    /// <summary>
    /// Convertit une durée en une formulation française abrégée (jours, heures, minutes).
    /// </summary>
    internal static string Duration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1)
            return "moins d'une minute";

        var parts = new List<string>();

        if (duration.Days > 0)
            parts.Add($"{duration.Days} {Plural(duration.Days, "jour", "jours")}");

        if (duration.Hours > 0)
            parts.Add($"{duration.Hours} {Plural(duration.Hours, "heure", "heures")}");

        // Les minutes ne sont annoncées que si la durée reste courte, pour éviter
        // une énumération inutilement longue sur une machine allumée depuis des semaines.
        if (duration.Minutes > 0 && duration.Days == 0)
            parts.Add($"{duration.Minutes} {Plural(duration.Minutes, "minute", "minutes")}");

        return parts.Count switch
        {
            0 => "moins d'une minute",
            1 => parts[0],
            _ => $"{string.Join(", ", parts.Take(parts.Count - 1))} et {parts[^1]}"
        };
    }
}
