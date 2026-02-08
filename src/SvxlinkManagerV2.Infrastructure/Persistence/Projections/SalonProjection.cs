using SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Projections;

/// <summary>
/// Projection Marten pour Salon.
/// Utilisée pour les queries performantes sans rehydrater tout l'aggregate.
/// </summary>
public class SalonProjection
{
    /// <summary>
    /// Identifiant du Salon
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Nom du salon
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Hôte du reflector
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Port du reflector
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Indicatif du nœud
    /// </summary>
    public string Callsign { get; set; } = string.Empty;

    /// <summary>
    /// Indique si c'est le salon par défaut
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Indique si le salon est temporisé
    /// </summary>
    public bool IsTemporized { get; set; }

    /// <summary>
    /// Indique si le salon est actuellement actif
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Date de création
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Date de dernière modification
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Indique si le salon est supprimé
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Fréquence de réception en MHz
    /// </summary>
    public decimal RxFrequency { get; set; }

    /// <summary>
    /// Fréquence de transmission en MHz
    /// </summary>
    public decimal TxFrequency { get; set; }

    /// <summary>
    /// Tonalité CTCSS de réception en Hz (nullable)
    /// </summary>
    public decimal? RxCtcss { get; set; }

    /// <summary>
    /// Tonalité CTCSS de transmission en Hz (nullable)
    /// </summary>
    public decimal? TxCtcss { get; set; }

    /// <summary>
    /// Identifiant du Sound associé (optionnel)
    /// </summary>
    public Guid? SoundId { get; set; }

    /// <summary>
    /// Applique l'événement SalonCreated
    /// </summary>
    public void Apply(SalonCreated @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        IsDefault = @event.IsDefault;
        IsTemporized = @event.IsTemporized;
        Host = @event.Configuration.Host;
        Port = @event.Configuration.Port;
        Callsign = @event.Configuration.Callsign;
        RxFrequency = @event.Configuration.RxFrequency;
        TxFrequency = @event.Configuration.TxFrequency;
        RxCtcss = @event.Configuration.RxCtcss;
        TxCtcss = @event.Configuration.TxCtcss;
        SoundId = @event.Configuration.SoundId;
        IsActive = false;
        IsDeleted = false;
        CreatedAt = @event.OccurredOn;
        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement SalonConfigurationUpdated
    /// </summary>
    public void Apply(SalonConfigurationUpdated @event)
    {
        Host = @event.Configuration.Host;
        Port = @event.Configuration.Port;
        Callsign = @event.Configuration.Callsign;
        RxFrequency = @event.Configuration.RxFrequency;
        TxFrequency = @event.Configuration.TxFrequency;
        RxCtcss = @event.Configuration.RxCtcss;
        TxCtcss = @event.Configuration.TxCtcss;
        SoundId = @event.Configuration.SoundId;
        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement SalonActivated
    /// </summary>
    public void Apply(SalonActivated @event)
    {
        IsActive = true;
        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement SalonDeactivated
    /// </summary>
    public void Apply(SalonDeactivated @event)
    {
        IsActive = false;
        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement SalonDeleted
    /// </summary>
    public void Apply(SalonDeleted @event)
    {
        IsDeleted = true;
        UpdatedAt = @event.OccurredOn;
    }
}
