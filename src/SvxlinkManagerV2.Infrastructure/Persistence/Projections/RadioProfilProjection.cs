using Marten.Events.Aggregation;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Events;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Projections;

/// <summary>
/// Projection Marten pour RadioProfil.
/// Utilisée pour les queries performantes sans rehydrater tout l'aggregate.
/// </summary>
public class RadioProfilProjection
{
    /// <summary>
    /// Identifiant du RadioProfil
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Nom du profil radio
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Fréquence CTCSS de réception (nullable)
    /// </summary>
    public decimal? RxCtcss { get; set; }

    /// <summary>
    /// Fréquence CTCSS de transmission (nullable)
    /// </summary>
    public decimal? TxCtcss { get; set; }

    /// <summary>
    /// Type de détection de squelch (GPIO, VOX, CTCSS, etc.)
    /// </summary>
    public string SqlDet { get; set; } = string.Empty;

    /// <summary>
    /// Date de création
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Date de dernière modification
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Indique si le profil est supprimé
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Applique l'événement RadioProfilCreatedEvent
    /// </summary>
    public void Apply(RadioProfilCreatedEvent @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        RxCtcss = @event.RxConfiguration.CtcssFq;
        TxCtcss = @event.TxConfiguration.CtcssFq;
        SqlDet = @event.RxConfiguration.SqlDet;
        CreatedAt = @event.OccurredOn;
        UpdatedAt = @event.OccurredOn;
        IsDeleted = false;
    }

    /// <summary>
    /// Applique l'événement RadioProfilUpdatedEvent
    /// </summary>
    public void Apply(RadioProfilUpdatedEvent @event)
    {
        if (@event.Name != null)
            Name = @event.Name;

        if (@event.RxConfiguration != null)
        {
            RxCtcss = @event.RxConfiguration.CtcssFq;
            SqlDet = @event.RxConfiguration.SqlDet;
        }

        if (@event.TxConfiguration != null)
            TxCtcss = @event.TxConfiguration.CtcssFq;

        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement RadioProfilDeletedEvent
    /// </summary>
    public void Apply(RadioProfilDeletedEvent @event)
    {
        IsDeleted = true;
        UpdatedAt = @event.OccurredOn;
    }
}
