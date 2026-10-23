using System.Globalization;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Presentation.Services;

/// <summary>
/// Service helper pour générer les options des dropdowns CTCSS.
/// </summary>
public static class CtcssDropdownService
{
    /// <summary>
    /// Obtient toutes les options CTCSS pour un dropdown (sans l'option "Aucun" qui doit être gérée séparément).
    /// </summary>
    /// <returns>Dictionnaire où la clé est la fréquence et la valeur est le label</returns>
    public static Dictionary<decimal, string> GetCtcssOptions()
    {
        var options = new Dictionary<decimal, string>();

        foreach (var frequency in CtcssMapper.GetAllFrequencies().OrderBy(f => f))
        {
            options.Add(frequency, $"{frequency:F1} Hz");
        }

        return options;
    }

    /// <summary>
    /// Obtient le label pour une fréquence CTCSS donnée.
    /// </summary>
    /// <param name="frequency">Fréquence CTCSS en Hz (null pour "Aucun")</param>
    /// <returns>Label formaté (ex: "136.5 Hz" ou "Aucun")</returns>
    public static string GetCtcssLabel(decimal? frequency)
    {
        return frequency.HasValue ? $"{frequency:F1} Hz" : "Aucun";
    }

    /// <summary>
    /// Valeur d'une option du dropdown, toujours en culture invariante.
    /// </summary>
    /// <remarks>
    /// Écrire la valeur dans la culture courante puis la relire en invariante transforme
    /// « 67,0 » en 670 : la virgule y est un séparateur de milliers (#166).
    /// </remarks>
    /// <param name="frequency">Fréquence CTCSS en Hz (null pour "Aucun")</param>
    /// <returns>Valeur de l'option (ex: "67.0", ou chaîne vide pour "Aucun")</returns>
    public static string ToOptionValue(decimal? frequency)
    {
        return frequency?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// Relit la valeur d'une option écrite par <see cref="ToOptionValue"/>.
    /// </summary>
    /// <param name="value">Valeur de l'option sélectionnée</param>
    /// <returns>Fréquence CTCSS en Hz, ou null pour "Aucun" ou une valeur illisible</returns>
    public static decimal? ParseOptionValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        return decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var frequency)
            ? frequency
            : null;
    }
}
