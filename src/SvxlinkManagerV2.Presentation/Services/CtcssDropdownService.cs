using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Presentation.Services;

/// <summary>
/// Service helper pour générer les options des dropdowns CTCSS.
/// </summary>
public static class CtcssDropdownService
{
    /// <summary>
    /// Obtient toutes les options CTCSS pour un dropdown (avec option "Aucun").
    /// </summary>
    /// <returns>Dictionnaire où la clé est la fréquence (null pour "Aucun") et la valeur est le label</returns>
    public static Dictionary<decimal?, string> GetCtcssOptions()
    {
        var options = new Dictionary<decimal?, string>
        {
            { null, "Aucun" }
        };

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
}
