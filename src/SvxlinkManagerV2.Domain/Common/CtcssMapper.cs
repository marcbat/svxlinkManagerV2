namespace SvxlinkManagerV2.Domain.Common;

/// <summary>
/// Mapper pour convertir les fréquences CTCSS (Hz) en codes SA818 et inversement.
/// Les codes SA818 vont de 0000 (pas de tone) à 0051 (250.3 Hz).
/// </summary>
public static class CtcssMapper
{
    /// <summary>
    /// Table de correspondance : Code SA818 → Fréquence CTCSS (Hz).
    /// Index 0 = "0000" (pas de tone), Index 1 = "0001" (67.0 Hz), etc.
    /// </summary>
    private static readonly decimal?[] CodeToFrequencyMap =
    {
        null,    // 0000 - Pas de tone
        67.0M,   // 0001
        71.9M,   // 0002
        74.4M,   // 0003
        77.0M,   // 0004
        79.7M,   // 0005
        82.5M,   // 0006
        85.4M,   // 0007
        88.5M,   // 0008
        91.5M,   // 0009
        94.8M,   // 0010
        97.4M,   // 0011
        100.0M,  // 0012
        103.5M,  // 0013
        107.2M,  // 0014
        110.9M,  // 0015
        114.8M,  // 0016
        118.8M,  // 0017
        123.0M,  // 0018
        127.3M,  // 0019
        131.8M,  // 0020
        136.5M,  // 0021
        141.3M,  // 0022
        146.2M,  // 0023
        151.4M,  // 0024
        156.7M,  // 0025
        162.2M,  // 0026
        167.9M,  // 0027
        173.8M,  // 0028
        179.9M,  // 0029
        186.2M,  // 0030
        192.8M,  // 0031
        203.5M,  // 0032
        210.7M,  // 0033
        218.1M,  // 0034
        225.7M,  // 0035
        233.6M,  // 0036
        241.8M,  // 0037
        250.3M   // 0038
    };

    /// <summary>
    /// Convertit une fréquence CTCSS (Hz) en code SA818 (format "0000" à "0038").
    /// </summary>
    /// <param name="frequencyHz">Fréquence CTCSS en Hz (ex: 136.5M). Null = pas de tone.</param>
    /// <returns>Code SA818 au format "0000" (si null ou invalide) ou "0001"-"0038"</returns>
    public static string FrequencyToCode(decimal? frequencyHz)
    {
        if (frequencyHz == null)
            return "0000";

        // Recherche de la fréquence dans le tableau
        for (int i = 1; i < CodeToFrequencyMap.Length; i++)
        {
            if (CodeToFrequencyMap[i] == frequencyHz)
                return i.ToString("D4");
        }

        // Si la fréquence n'est pas trouvée exactement, retourner "0000"
        return "0000";
    }

    /// <summary>
    /// Convertit un code SA818 (format "0000" à "0038") en fréquence CTCSS (Hz).
    /// </summary>
    /// <param name="code">Code SA818 au format "0000"-"0038"</param>
    /// <returns>Fréquence CTCSS en Hz ou null si code = "0000" ou invalide</returns>
    public static decimal? CodeToFrequencyHz(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        if (!int.TryParse(code, out int index))
            return null;

        if (index < 0 || index >= CodeToFrequencyMap.Length)
            return null;

        return CodeToFrequencyMap[index];
    }

    /// <summary>
    /// Valide si une fréquence CTCSS est supportée par le SA818.
    /// </summary>
    /// <param name="frequencyHz">Fréquence à valider</param>
    /// <returns>True si la fréquence est valide (ou null), false sinon</returns>
    public static bool IsValidFrequency(decimal? frequencyHz)
    {
        if (frequencyHz == null)
            return true;

        return Array.IndexOf(CodeToFrequencyMap, frequencyHz) != -1;
    }

    /// <summary>
    /// Obtient toutes les fréquences CTCSS supportées (sans le null/"0000").
    /// </summary>
    /// <returns>Liste des fréquences CTCSS valides</returns>
    public static IEnumerable<decimal> GetAllFrequencies()
    {
        return CodeToFrequencyMap.Where(f => f.HasValue).Select(f => f!.Value);
    }
}
