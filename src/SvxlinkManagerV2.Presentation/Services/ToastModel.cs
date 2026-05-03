namespace SvxlinkManagerV2.Presentation.Services;

/// <summary>
/// Modèle représentant un toast de notification
/// </summary>
public class ToastModel
{
    /// <summary>
    /// Identifiant unique du toast
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Type du toast (Success, Error, Info, Warning)
    /// </summary>
    public ToastType Type { get; init; }

    /// <summary>
    /// Message principal du toast
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Titre optionnel du toast
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Durée d'affichage en millisecondes (0 = pas d'auto-dismiss)
    /// </summary>
    public int DurationMs { get; init; } = 3000;

    /// <summary>
    /// Date/heure de création du toast
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// Type de toast de notification
/// </summary>
public enum ToastType
{
    /// <summary>
    /// Toast de succès (vert)
    /// </summary>
    Success,

    /// <summary>
    /// Toast d'erreur (rouge)
    /// </summary>
    Error,

    /// <summary>
    /// Toast d'information (bleu)
    /// </summary>
    Info,

    /// <summary>
    /// Toast d'avertissement (jaune/orange)
    /// </summary>
    Warning
}
