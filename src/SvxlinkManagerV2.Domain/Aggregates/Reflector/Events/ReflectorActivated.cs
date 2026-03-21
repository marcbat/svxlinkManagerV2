using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;

/// <summary>
/// Événement émis lors de l'activation du Reflector.
/// L'activation signifie que le daemon svxreflector est démarré.
/// </summary>
public record ReflectorActivated : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Reflector activé
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public ReflectorActivated(Guid id)
    {
        Id = id;
    }
}
