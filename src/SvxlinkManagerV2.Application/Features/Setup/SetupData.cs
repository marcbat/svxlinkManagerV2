namespace SvxlinkManagerV2.Application.Features.Setup;

/// <summary>
/// Données collectées par le wizard de configuration initiale.
/// Transportées entre les étapes via <c>SetupWizardState</c> et envoyées au handler en une seule commande.
/// </summary>
public record SetupData
{
    /// <summary>
    /// Indicatif radioamateur utilisé pour s'authentifier auprès du réflecteur SVXLink (ex: "F5ABC").
    /// </summary>
    public string Callsign { get; init; } = string.Empty;

    /// <summary>
    /// Indicatif de la station simplex locale affiché dans les annonces (ex: "F5ABC-L").
    /// </summary>
    public string SimplexCallsign { get; init; } = string.Empty;

    /// <summary>
    /// Fréquence de réception par défaut en MHz (ex: 145.550).
    /// </summary>
    public decimal RxFrequency { get; init; } = 145.550m;

    /// <summary>
    /// Fréquence d'émission par défaut en MHz (ex: 145.550).
    /// </summary>
    public decimal TxFrequency { get; init; } = 145.550m;

    /// <summary>
    /// Fréquence CTCSS de réception en Hz (null = pas de CTCSS).
    /// </summary>
    public decimal? RxCtcss { get; init; }

    /// <summary>
    /// Fréquence CTCSS d'émission en Hz (null = pas de CTCSS).
    /// </summary>
    public decimal? TxCtcss { get; init; }
}
