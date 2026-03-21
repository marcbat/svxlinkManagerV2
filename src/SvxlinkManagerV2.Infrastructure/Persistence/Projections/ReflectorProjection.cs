using SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Projections;

/// <summary>
/// Projection Marten pour Reflector.
/// Utilisée pour les queries performantes sans rehydrater tout l'aggregate.
/// </summary>
public class ReflectorProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Config { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public void Apply(ReflectorCreated @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        Config = @event.Config;
        IsDeleted = false;
        CreatedAt = @event.OccurredOn;
        UpdatedAt = @event.OccurredOn;
    }

    public void Apply(ReflectorConfigurationUpdated @event)
    {
        Name = @event.Name;
        Config = @event.Config;
        UpdatedAt = @event.OccurredOn;
    }

    public void Apply(ReflectorDeleted @event)
    {
        IsDeleted = true;
        UpdatedAt = @event.OccurredOn;
    }
}
