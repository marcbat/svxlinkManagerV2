using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.SA818.Events;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Projections;

/// <summary>
/// Projection Marten pour SA818.
/// Utilisée pour les queries performantes sans rehydrater tout l'aggregate.
/// Il n'existe qu'une seule instance de cette projection (ID fixe).
/// </summary>
public class SA818Projection
{
    /// <summary>
    /// Identifiant du SA818 (ID fixe : 00000000-0000-0000-0000-000000000001)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Volume audio (plage valide: 1-8)
    /// </summary>
    public int Volume { get; set; }

    /// <summary>
    /// Niveau de squelch (plage valide: 0-8)
    /// </summary>
    public int Squelch { get; set; }

    /// <summary>
    /// Largeur de bande (12.5kHz ou 25kHz)
    /// </summary>
    public SA818Bandwidth Bandwidth { get; set; }

    /// <summary>
    /// Activation du filtre de pré-accentuation audio
    /// </summary>
    public bool PreEmph { get; set; }

    /// <summary>
    /// Activation du filtre passe-haut
    /// </summary>
    public bool HighPass { get; set; }

    /// <summary>
    /// Activation du filtre passe-bas
    /// </summary>
    public bool LowPass { get; set; }

    /// <summary>
    /// Date de dernière modification
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Applique l'événement SA818ConfigurationUpdatedEvent
    /// </summary>
    public void Apply(SA818ConfigurationUpdatedEvent @event)
    {
        Id = @event.Id;
        Volume = @event.Volume;
        Squelch = @event.Squelch;
        Bandwidth = @event.Bandwidth;
        PreEmph = @event.PreEmph;
        HighPass = @event.HighPass;
        LowPass = @event.LowPass;
        UpdatedAt = @event.OccurredOn;
    }
}
