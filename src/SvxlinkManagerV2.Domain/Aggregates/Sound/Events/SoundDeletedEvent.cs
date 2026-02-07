using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Sound.Events;

/// <summary>
/// Événement émis lors de la suppression d'un Sound
/// </summary>
public record SoundDeletedEvent : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Sound supprimé
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public SoundDeletedEvent(Guid id)
    {
        Id = id;
    }
}
