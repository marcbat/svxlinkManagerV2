using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

/// <summary>
/// Événement émis lors de la désactivation d'un Salon.
/// La désactivation signifie que le Salon n'est plus actif et que SVXLink se déconnecte du reflector.
/// </summary>
public record SalonDeactivated : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Salon désactivé
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public SalonDeactivated(Guid id)
    {
        Id = id;
    }
}
