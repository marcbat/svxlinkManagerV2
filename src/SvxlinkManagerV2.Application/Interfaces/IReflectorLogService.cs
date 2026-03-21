using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de gestion du buffer de logs du daemon svxreflector.
/// Réutilise le modèle SvxLinkLogEntry pour la cohérence avec les logs SVXLink.
/// </summary>
public interface IReflectorLogService
{
    /// <summary>
    /// Retourne la liste des logs actuellement en buffer
    /// </summary>
    IReadOnlyList<SvxLinkLogEntry> GetLogs();

    /// <summary>
    /// Nombre maximum de lignes conservées en buffer (100 à 10000)
    /// </summary>
    int MaxLines { get; set; }

    /// <summary>
    /// Vide le buffer de logs
    /// </summary>
    void Clear();

    /// <summary>
    /// Ajoute une ligne brute au buffer (parsée et enrichie avec timestamp et niveau)
    /// </summary>
    /// <param name="rawLine">Ligne brute de log</param>
    void AddLog(string rawLine);

    /// <summary>
    /// Événement déclenché à chaque nouvelle entrée de log (temps réel vers l'UI Blazor)
    /// </summary>
    event Action<SvxLinkLogEntry>? OnLogReceived;
}
