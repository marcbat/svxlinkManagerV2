using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

/// <summary>
/// Événement émis lors de l'activation d'un Salon.
/// L'activation signifie que le Salon devient le salon actif et que SVXLink se connecte au reflector.
/// </summary>
public record SalonActivated : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Salon activé
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public SalonActivated(Guid id)
    {
        Id = id;
    }
}
