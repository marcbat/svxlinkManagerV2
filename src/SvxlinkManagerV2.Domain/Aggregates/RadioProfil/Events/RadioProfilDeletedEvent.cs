using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Events;

/// <summary>
/// Événement émis lors de la suppression d'un RadioProfil
/// </summary>
public record RadioProfilDeletedEvent : DomainEvent
{
    /// <summary>
    /// Identifiant du RadioProfil supprimé
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public RadioProfilDeletedEvent(Guid id)
    {
        Id = id;
    }
}
