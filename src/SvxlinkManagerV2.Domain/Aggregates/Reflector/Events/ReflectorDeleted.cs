using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;

/// <summary>
/// Événement émis lors de la suppression d'un Reflector (soft delete).
/// Le Reflector reste dans l'historique mais n'est plus utilisable.
/// </summary>
public record ReflectorDeleted : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Reflector supprimé
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public ReflectorDeleted(Guid id)
    {
        Id = id;
    }
}
