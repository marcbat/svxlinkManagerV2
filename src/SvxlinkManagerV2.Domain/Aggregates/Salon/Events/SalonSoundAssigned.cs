using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

/// <summary>
/// Événement émis lors de l'assignation d'un son à un salon
/// </summary>
public record SalonSoundAssigned : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Salon
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Identifiant du son assigné au salon
    /// </summary>
    public Guid SoundId { get; init; }

    public SalonSoundAssigned(Guid id, Guid soundId)
    {
        Id = id;
        SoundId = soundId;
    }
}
