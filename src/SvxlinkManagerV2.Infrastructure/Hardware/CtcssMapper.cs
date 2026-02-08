using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Hardware;

/// <summary>
/// Mapper statique pour convertir les fréquences CTCSS (Hz) en codes SA818.
/// Le module SA818 utilise des codes numériques ("0001" à "0038") pour identifier 
/// les tonalités CTCSS dans les commandes AT.
/// </summary>
/// <remarks>
/// Exemples de mapping :
/// <code>
/// - 67.0M  → "0001"
/// - 136.5M → "0021"
/// - 250.3M → "0038"
/// - null ou 0M → "0000" (aucun CTCSS)
/// </code>
/// </remarks>
public static class CtcssMapper
{
    /// <summary>
    /// Dictionnaire de mapping entre les fréquences CTCSS standard (Hz) et les codes SA818.
    /// Contient les 38 valeurs CTCSS standard de 67.0 Hz à 250.3 Hz.
    /// </summary>
    private static readonly Dictionary<decimal, string> CtcssCodes = new()
    {
        { 67.0m, "0001" },
        { 71.9m, "0002" },
        { 74.4m, "0003" },
        { 77.0m, "0004" },
        { 79.7m, "0005" },
        { 82.5m, "0006" },
        { 85.4m, "0007" },
        { 88.5m, "0008" },
        { 91.5m, "0009" },
        { 94.8m, "0010" },
        { 97.4m, "0011" },
        { 100.0m, "0012" },
        { 103.5m, "0013" },
        { 107.2m, "0014" },
        { 110.9m, "0015" },
        { 114.8m, "0016" },
        { 118.8m, "0017" },
        { 123.0m, "0018" },
        { 127.3m, "0019" },
        { 131.8m, "0020" },
        { 136.5m, "0021" },
        { 141.3m, "0022" },
        { 146.2m, "0023" },
        { 151.4m, "0024" },
        { 156.7m, "0025" },
        { 162.2m, "0026" },
        { 167.9m, "0027" },
        { 173.8m, "0028" },
        { 179.9m, "0029" },
        { 186.2m, "0030" },
        { 192.8m, "0031" },
        { 203.5m, "0032" },
        { 210.7m, "0033" },
        { 218.1m, "0034" },
        { 225.7m, "0035" },
        { 233.6m, "0036" },
        { 241.8m, "0037" },
        { 250.3m, "0038" }
    };

    /// <summary>
    /// Convertit une fréquence CTCSS (Hz) en code SA818.
    /// </summary>
    /// <param name="ctcssHz">
    /// Fréquence CTCSS en Hz (decimal nullable). 
    /// Si null ou 0, retourne "0000" (aucun CTCSS).
    /// </param>
    /// <returns>
    /// Validation contenant le code SA818 si la fréquence est valide,
    /// ou une erreur si la fréquence n'est pas dans le dictionnaire des valeurs standard.
    /// </returns>
    /// <example>
    /// <code>
    /// var result1 = CtcssMapper.ToSA818Code(136.5m);  // Success("0021")
    /// var result2 = CtcssMapper.ToSA818Code(null);    // Success("0000")
    /// var result3 = CtcssMapper.ToSA818Code(999.9m);  // Fail(Error)
    /// </code>
    /// </example>
    public static Validation<Error, string> ToSA818Code(decimal? ctcssHz)
    {
        // Cas null ou 0 : aucun CTCSS
        if (!ctcssHz.HasValue || ctcssHz.Value == 0m)
        {
            return Success<Error, string>("0000");
        }

        // Recherche dans le dictionnaire
        if (CtcssCodes.TryGetValue(ctcssHz.Value, out var code))
        {
            return Success<Error, string>(code);
        }

        // Fréquence invalide
        return Fail<Error, string>(
            Error.New($"Fréquence CTCSS invalide : {ctcssHz.Value} Hz. " +
                     $"Les valeurs autorisées sont comprises entre 67.0 Hz et 250.3 Hz " +
                     $"(38 valeurs CTCSS standard).")
        );
    }
}
