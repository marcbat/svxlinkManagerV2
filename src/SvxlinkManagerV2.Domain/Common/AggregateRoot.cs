namespace SvxlinkManagerV2.Domain.Common;

/// <summary>
/// Classe de base pour tous les Aggregate Roots du domaine.
/// Un Aggregate Root est le point d'entrée transactionnel d'un ensemble d'entités liées.
/// Dans Event Sourcing, chaque Aggregate Root possède son propre stream d'événements dans Marten.
/// </summary>
/// <typeparam name="TId">Type de l'identifiant de l'Aggregate</typeparam>
public abstract class AggregateRoot<TId> where TId : notnull
{
    private readonly List<DomainEvent> _domainEvents = new();

    /// <summary>
    /// Constructeur par défaut protégé
    /// </summary>
    protected AggregateRoot()
    {
        Id = default!;
    }

    /// <summary>
    /// Constructeur avec identifiant
    /// </summary>
    /// <param name="id">Identifiant de l'aggregate</param>
    protected AggregateRoot(TId id)
    {
        Id = id;
    }

    /// <summary>
    /// Identifiant unique de l'Aggregate Root
    /// </summary>
    public TId Id { get; protected set; }

    /// <summary>
    /// Collection d'événements du domaine non commités
    /// </summary>
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Ajoute un événement du domaine à la collection
    /// </summary>
    /// <param name="domainEvent">Événement à ajouter</param>
    protected void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Vide la collection d'événements non commités
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// Aggregate Root avec Guid comme identifiant par défaut.
/// Utilisé par défaut dans Event Sourcing avec Marten.
/// </summary>
public abstract class AggregateRoot : AggregateRoot<Guid>
{
    /// <summary>
    /// Constructeur par défaut
    /// </summary>
    protected AggregateRoot() : base()
    {
    }

    /// <summary>
    /// Constructeur avec identifiant
    /// </summary>
    /// <param name="id">Identifiant Guid de l'aggregate</param>
    protected AggregateRoot(Guid id) : base(id)
    {
    }
}
