using SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Projections;

/// <summary>
/// Projection Marten pour Reflector.
/// Utilisée pour les queries performantes sans rehydrater tout l'aggregate.
/// </summary>
public class ReflectorProjection
{
    /// <summary>
    /// Identifiant du Reflector
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Nom descriptif du reflector
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Contenu brut du fichier de configuration INI svxreflector.conf
    /// </summary>
    public string Config { get; set; } = string.Empty;

    /// <summary>
    /// Indique si le daemon svxreflector est actuellement actif
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Indique si le reflector est supprimé (soft delete)
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Date de création
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Date de dernière modification
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Applique l'événement ReflectorCreated
    /// </summary>
    public void Apply(ReflectorCreated @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        Config = @event.Config;
        IsActive = false;
        IsDeleted = false;
        CreatedAt = @event.OccurredOn;
        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement ReflectorConfigurationUpdated
    /// </summary>
    public void Apply(ReflectorConfigurationUpdated @event)
    {
        Name = @event.Name;
        Config = @event.Config;
        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement ReflectorActivated
    /// </summary>
    public void Apply(ReflectorActivated @event)
    {
        IsActive = true;
        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement ReflectorDeactivated
    /// </summary>
    public void Apply(ReflectorDeactivated @event)
    {
        IsActive = false;
        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement ReflectorDeleted
    /// </summary>
    public void Apply(ReflectorDeleted @event)
    {
        IsDeleted = true;
        UpdatedAt = @event.OccurredOn;
    }
}
