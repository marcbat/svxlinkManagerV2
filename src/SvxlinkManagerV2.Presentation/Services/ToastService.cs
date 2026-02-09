namespace SvxlinkManagerV2.Presentation.Services;

/// <summary>
/// Service singleton pour gérer les toasts de notification dans l'application
/// </summary>
public class ToastService
{
    private readonly List<ToastModel> _toasts = new();
    private const int MaxToasts = 5;

    /// <summary>
    /// Event déclenché quand un nouveau toast est ajouté
    /// </summary>
    public event Action? OnToastAdded;

    /// <summary>
    /// Event déclenché quand un toast est retiré
    /// </summary>
    public event Action? OnToastRemoved;

    /// <summary>
    /// Liste des toasts actifs (lecture seule)
    /// </summary>
    public IReadOnlyList<ToastModel> Toasts => _toasts.AsReadOnly();

    /// <summary>
    /// Affiche un toast de succès
    /// </summary>
    /// <param name="message">Message à afficher</param>
    /// <param name="title">Titre optionnel</param>
    /// <param name="durationMs">Durée d'affichage en millisecondes (défaut: 3000)</param>
    public void ShowSuccess(string message, string? title = null, int durationMs = 3000)
    {
        AddToast(new ToastModel
        {
            Type = ToastType.Success,
            Message = message,
            Title = title ?? "Succès",
            DurationMs = durationMs
        });
    }

    /// <summary>
    /// Affiche un toast d'erreur
    /// </summary>
    /// <param name="message">Message à afficher</param>
    /// <param name="title">Titre optionnel</param>
    /// <param name="durationMs">Durée d'affichage en millisecondes (défaut: 3000)</param>
    public void ShowError(string message, string? title = null, int durationMs = 3000)
    {
        AddToast(new ToastModel
        {
            Type = ToastType.Error,
            Message = message,
            Title = title ?? "Erreur",
            DurationMs = durationMs
        });
    }

    /// <summary>
    /// Affiche un toast d'information
    /// </summary>
    /// <param name="message">Message à afficher</param>
    /// <param name="title">Titre optionnel</param>
    /// <param name="durationMs">Durée d'affichage en millisecondes (défaut: 3000)</param>
    public void ShowInfo(string message, string? title = null, int durationMs = 3000)
    {
        AddToast(new ToastModel
        {
            Type = ToastType.Info,
            Message = message,
            Title = title ?? "Information",
            DurationMs = durationMs
        });
    }

    /// <summary>
    /// Affiche un toast d'avertissement
    /// </summary>
    /// <param name="message">Message à afficher</param>
    /// <param name="title">Titre optionnel</param>
    /// <param name="durationMs">Durée d'affichage en millisecondes (défaut: 3000)</param>
    public void ShowWarning(string message, string? title = null, int durationMs = 3000)
    {
        AddToast(new ToastModel
        {
            Type = ToastType.Warning,
            Message = message,
            Title = title ?? "Attention",
            DurationMs = durationMs
        });
    }

    /// <summary>
    /// Ferme tous les toasts actifs
    /// </summary>
    public void Clear()
    {
        _toasts.Clear();
        OnToastRemoved?.Invoke();
    }

    /// <summary>
    /// Retire un toast spécifique par son ID
    /// </summary>
    /// <param name="toastId">ID du toast à retirer</param>
    public void Remove(Guid toastId)
    {
        var toast = _toasts.FirstOrDefault(t => t.Id == toastId);
        if (toast != null)
        {
            _toasts.Remove(toast);
            OnToastRemoved?.Invoke();
        }
    }

    /// <summary>
    /// Ajoute un nouveau toast à la queue
    /// Respecte la limite de MaxToasts (FIFO)
    /// </summary>
    /// <param name="toast">Toast à ajouter</param>
    private void AddToast(ToastModel toast)
    {
        // Si on dépasse la limite, retirer le plus ancien (FIFO)
        if (_toasts.Count >= MaxToasts)
        {
            _toasts.RemoveAt(0);
        }

        _toasts.Add(toast);
        OnToastAdded?.Invoke();
    }
}
