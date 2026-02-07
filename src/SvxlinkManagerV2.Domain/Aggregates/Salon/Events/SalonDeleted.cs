using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

/// <summary>
/// Événement émis lors de la suppression d'un Salon (soft delete).
/// Le Salon reste dans l'historique mais n'est plus utilisable.
/// </summary>
public record SalonDeleted : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Salon supprimé
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public SalonDeleted(Guid id)
    {
        Id = id;
    }
}
