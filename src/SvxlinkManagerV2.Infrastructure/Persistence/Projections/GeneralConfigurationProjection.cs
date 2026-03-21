using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Events;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Projections;

/// <summary>
/// Projection Marten pour la configuration générale.
/// Utilisée pour les queries performantes sans rehydrater tout l'aggregate.
/// Il n'existe qu'une seule instance de cette projection (ID fixe).
/// </summary>
public class GeneralConfigurationProjection
{
    public Guid Id { get; set; }
    public bool StartReflectorOnStartup { get; set; }
    public bool StartDefaultSalonOnStartup { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Applique l'événement GeneralConfigurationCreated
    /// </summary>
    public void Apply(GeneralConfigurationCreated @event)
    {
        Id = @event.Id;
        StartReflectorOnStartup = @event.StartReflectorOnStartup;
        StartDefaultSalonOnStartup = @event.StartDefaultSalonOnStartup;
        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement GeneralConfigurationUpdated
    /// </summary>
    public void Apply(GeneralConfigurationUpdated @event)
    {
        StartReflectorOnStartup = @event.StartReflectorOnStartup;
        StartDefaultSalonOnStartup = @event.StartDefaultSalonOnStartup;
        UpdatedAt = @event.OccurredOn;
    }
}
