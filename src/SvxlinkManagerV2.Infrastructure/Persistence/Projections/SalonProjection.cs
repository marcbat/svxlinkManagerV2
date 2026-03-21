using SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Projections;

/// <summary>
/// Projection Marten pour Salon.
/// Utilisée pour les queries performantes sans rehydrater tout l'aggregate.
/// </summary>
public class SalonProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Callsign { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsTemporized { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public decimal RxFrequency { get; set; }
    public decimal TxFrequency { get; set; }
    public decimal? RxCtcss { get; set; }
    public decimal? TxCtcss { get; set; }
    public Guid? SoundId { get; set; }

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
        IsDeleted = false;
        CreatedAt = @event.OccurredOn;
        UpdatedAt = @event.OccurredOn;
    }

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

    public void Apply(SalonDeleted @event)
    {
        IsDeleted = true;
        UpdatedAt = @event.OccurredOn;
    }

    public void Apply(SalonSetAsDefault @event)
    {
        IsDefault = true;
        UpdatedAt = @event.OccurredOn;
    }

    public void Apply(SalonUnsetDefault @event)
    {
        IsDefault = false;
        UpdatedAt = @event.OccurredOn;
    }
}

