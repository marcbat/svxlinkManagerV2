using System.Globalization;

namespace SvxlinkManagerV2.Presentation.Services;

/// <summary>
/// Mise en forme des nombres destinés à des styles CSS en ligne.
///
/// Le CSS n'accepte que le point décimal. Sous une culture française ou suisse — celle du
/// serveur comme du navigateur — une interpolation naïve produit « width:74,0% » ou
/// « rgba(108, 92, 231, 0,66) », que le navigateur rejette **en silence** : la barre reste
/// à zéro, la case reste transparente, et rien ne signale l'erreur. D'où le passage obligé
/// par la culture invariante.
/// </summary>
public static class CssValue
{
    /// <summary>
    /// Largeur en pourcentage, bornée à l'intervalle 0-100.
    /// </summary>
    /// <param name="percent">Part à représenter.</param>
    public static string Percent(double percent)
        => Number(Math.Clamp(percent, 0, 100), "F1") + "%";

    /// <summary>
    /// Couleur d'accentuation à opacité variable, l'opacité étant bornée à 0-1.
    /// </summary>
    /// <param name="red">Composante rouge.</param>
    /// <param name="green">Composante verte.</param>
    /// <param name="blue">Composante bleue.</param>
    /// <param name="alpha">Opacité, de 0 (transparent) à 1 (opaque).</param>
    public static string Rgba(int red, int green, int blue, double alpha)
        => $"rgba({red}, {green}, {blue}, {Number(Math.Clamp(alpha, 0, 1), "F2")})";

    private static string Number(double value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture);
}
