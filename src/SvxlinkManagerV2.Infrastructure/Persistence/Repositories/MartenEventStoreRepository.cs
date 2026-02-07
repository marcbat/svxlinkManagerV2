using Marten;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implémentation du repository Event Sourcing avec Marten
/// </summary>
public class MartenEventStoreRepository : IEventStoreRepository
{
    private readonly IDocumentSession _session;

    public MartenEventStoreRepository(IDocumentSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public Task AppendEventsAsync<TAggregate>(Guid aggregateId, params DomainEvent[] events)
        where TAggregate : AggregateRoot
    {
        if (aggregateId == Guid.Empty)
            throw new ArgumentException("AggregateId ne peut pas être vide", nameof(aggregateId));
        
        if (events == null || events.Length == 0)
            throw new ArgumentException("Au moins un événement est requis", nameof(events));

        // Append les événements au stream de l'aggregate
        _session.Events.Append(aggregateId, events);
        
        return Task.CompletedTask;
    }

    public async Task<TAggregate?> LoadAggregateAsync<TAggregate>(Guid aggregateId)
        where TAggregate : AggregateRoot, new()
    {
        if (aggregateId == Guid.Empty)
            return null;

        // Recharge l'aggregate depuis son stream d'événements
        var aggregate = await _session.Events.AggregateStreamAsync<TAggregate>(aggregateId);
        
        return aggregate;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _session.SaveChangesAsync(cancellationToken);
    }
}
