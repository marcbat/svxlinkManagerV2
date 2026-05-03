using SvxlinkManagerV2.Domain.Aggregates.SA818;

namespace SvxlinkManagerV2.Application.Features.SA818;

/// <summary>
/// DTO représentant la configuration actuelle du module SA818.
/// Utilisé pour les queries (lecture seule).
/// </summary>
public record SA818ConfigurationDto
{
    /// <summary>
    /// Identifiant du SA818 (ID fixe : 00000000-0000-0000-0000-000000000001)
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Volume audio (plage valide: 1-8)
    /// </summary>
    public int Volume { get; init; }

    /// <summary>
    /// Niveau de squelch (plage valide: 0-8)
    /// </summary>
    public int Squelch { get; init; }

    /// <summary>
    /// Largeur de bande (12.5kHz ou 25kHz)
    /// </summary>
    public SA818Bandwidth Bandwidth { get; init; }

    /// <summary>
    /// Activation du filtre de pré-accentuation audio
    /// </summary>
    public bool PreEmph { get; init; }

    /// <summary>
    /// Activation du filtre passe-haut
    /// </summary>
    public bool HighPass { get; init; }

    /// <summary>
    /// Activation du filtre passe-bas
    /// </summary>
    public bool LowPass { get; init; }

    /// <summary>
    /// Date de dernière modification
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Fréquence de réception en MHz (ex: 145.550). Null si aucun salon actif.
    /// </summary>
    public decimal? RxFrequency { get; init; }

    /// <summary>
    /// Fréquence de transmission en MHz (ex: 145.550). Null si aucun salon actif.
    /// </summary>
    public decimal? TxFrequency { get; init; }

    /// <summary>
    /// Tonalité CTCSS de réception en Hz (ex: 136.5). Null si absente ou aucun salon actif.
    /// </summary>
    public decimal? RxCtcss { get; init; }

    /// <summary>
    /// Tonalité CTCSS de transmission en Hz (ex: 136.5). Null si absente ou aucun salon actif.
    /// </summary>
    public decimal? TxCtcss { get; init; }
}
