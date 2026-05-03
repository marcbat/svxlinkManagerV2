namespace SvxlinkManagerV2.Domain.Common;

/// <summary>
/// Classe de base pour tous les événements du domaine.
/// Un événement représente un fait qui s'est produit dans le passé (immutable).
/// Dans Event Sourcing, les événements sont la source de vérité pour reconstruire l'état des Aggregates.
/// </summary>
public abstract record DomainEvent
{
    /// <summary>
    /// Constructeur par défaut initialisant la date de l'événement
    /// </summary>
    protected DomainEvent()
    {
        OccurredOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Date et heure UTC de l'occurrence de l'événement
    /// </summary>
    public DateTime OccurredOn { get; init; }

    /// <summary>
    /// Identifiant unique de l'événement (généré automatiquement par Marten)
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();
}
