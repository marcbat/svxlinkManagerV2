using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

/// <summary>
/// Événement émis lors de la suppression du son d'un salon
/// </summary>
public record SalonSoundRemoved : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Salon
    /// </summary>
    public Guid Id { get; init; }

    public SalonSoundRemoved(Guid id)
    {
        Id = id;
    }
}
