using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

/// <summary>
/// Événement émis lorsqu'un Salon est désigné comme salon par défaut.
/// Un seul salon peut être le salon par défaut à la fois.
/// </summary>
public record SalonSetAsDefault : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Salon défini comme salon par défaut
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public SalonSetAsDefault(Guid id)
    {
        Id = id;
    }
}
