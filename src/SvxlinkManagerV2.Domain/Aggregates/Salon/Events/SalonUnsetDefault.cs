using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

/// <summary>
/// Événement émis lorsqu'un Salon perd son statut de salon par défaut.
/// Émis automatiquement sur l'ancien salon par défaut lors de la désignation d'un nouveau.
/// </summary>
public record SalonUnsetDefault : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Salon qui perd son statut de salon par défaut
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public SalonUnsetDefault(Guid id)
    {
        Id = id;
    }
}
