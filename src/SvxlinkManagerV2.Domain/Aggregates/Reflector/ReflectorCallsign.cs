using System.Text.RegularExpressions;

namespace SvxlinkManagerV2.Domain.Aggregates.Reflector;

/// <summary>
/// Validation d'un indicatif destiné à une commande du réflecteur.
/// </summary>
/// <remarks>
/// Les commandes partent dans un pseudo-terminal que le démon lit ligne par ligne : un
/// indicatif venu d'une demande de certificat, donc d'un tiers, ne doit pas pouvoir y glisser
/// d'espace ni de séparateur et transformer <c>CA SIGN X</c> en autre chose. Le jeu de
/// caractères retenu est celui qu'accepte un indicatif amateur, suffixes compris.
/// </remarks>
public static class ReflectorCallsign
{
    private static readonly Regex Pattern =
        new("^[A-Za-z0-9/-]{1,32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Indique si l'indicatif peut être transmis tel quel au réflecteur.</summary>
    public static bool IsValid(string? callsign) =>
        !string.IsNullOrWhiteSpace(callsign) && Pattern.IsMatch(callsign.Trim());
}
