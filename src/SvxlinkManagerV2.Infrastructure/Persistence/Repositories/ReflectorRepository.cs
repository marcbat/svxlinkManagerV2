using LanguageExt;
using Marten;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository pour la gestion du Reflector avec Event Sourcing
/// </summary>
public class ReflectorRepository : IReflectorRepository
{
    private readonly IDocumentSession _session;

    public ReflectorRepository(IDocumentSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async Task<Validation<Error, Unit>> SaveAsync(
        ReflectorAggregate aggregate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (aggregate.Id == Guid.Empty)
                return Error.Validation("INVALID_AGGREGATE_ID", "L'identifiant de l'aggregate est vide")
                    .ToFailure<Unit>();

            var events = aggregate.DomainEvents.ToArray();
            if (events.Length == 0)
                return unit.ToSuccess();

            _session.Events.Append(aggregate.Id, events);
            await _session.SaveChangesAsync(cancellationToken);
            aggregate.ClearDomainEvents();

            return unit.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("SAVE_ERROR", $"Erreur lors de la sauvegarde : {ex.Message}")
                .ToFailure<Unit>();
        }
    }

    public async Task<Validation<Error, ReflectorAggregate>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id == Guid.Empty)
                return Error.Validation("INVALID_ID", "L'identifiant est vide")
                    .ToFailure<ReflectorAggregate>();

            var aggregate = await _session.Events.AggregateStreamAsync<ReflectorAggregate>(id, token: cancellationToken);

            if (aggregate == null || aggregate.Id == Guid.Empty)
                return Error.NotFound("Reflector", id)
                    .ToFailure<ReflectorAggregate>();

            return aggregate.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("LOAD_ERROR", $"Erreur lors du chargement : {ex.Message}")
                .ToFailure<ReflectorAggregate>();
        }
    }

    public async Task<IReadOnlyList<ReflectorAggregate>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projections = await _session.Query<Projections.ReflectorProjection>()
                .Where(p => !p.IsDeleted)
                .ToListAsync(cancellationToken);

            var aggregates = new List<ReflectorAggregate>();
            foreach (var projection in projections)
            {
                var aggregate = await _session.Events.AggregateStreamAsync<ReflectorAggregate>(
                    projection.Id,
                    token: cancellationToken);

                if (aggregate != null && aggregate.Id != Guid.Empty)
                    aggregates.Add(aggregate);
            }

            return aggregates.AsReadOnly();
        }
        catch (Exception)
        {
            return System.Array.Empty<ReflectorAggregate>();
        }
    }

    public async Task<Validation<Error, Unit>> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var aggregateResult = await GetByIdAsync(id, cancellationToken);

        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        var deleteResult = aggregate.Delete();

        if (deleteResult.IsFail)
            return deleteResult;

        return await SaveAsync(aggregate, cancellationToken);
    }
}
