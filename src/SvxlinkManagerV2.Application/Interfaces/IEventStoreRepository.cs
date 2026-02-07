using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Repository pour la persistance Event Sourcing avec Marten
/// </summary>
public interface IEventStoreRepository
{
    /// <summary>
    /// Sauvegarde un ou plusieurs événements pour un aggregate
    /// </summary>
    Task AppendEventsAsync<TAggregate>(Guid aggregateId, params DomainEvent[] events) 
        where TAggregate : AggregateRoot;
    
    /// <summary>
    /// Recharge un aggregate depuis son stream d'événements
    /// </summary>
    Task<TAggregate?> LoadAggregateAsync<TAggregate>(Guid aggregateId) 
        where TAggregate : AggregateRoot, new();
    
    /// <summary>
    /// Sauvegarde les changements (commit)
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
