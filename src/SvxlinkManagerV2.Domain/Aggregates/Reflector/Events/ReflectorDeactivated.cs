using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;

/// <summary>
/// Événement émis lors de la désactivation du Reflector.
/// La désactivation signifie que le daemon svxreflector est arrêté.
/// </summary>
public record ReflectorDeactivated : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Reflector désactivé
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public ReflectorDeactivated(Guid id)
    {
        Id = id;
    }
}
