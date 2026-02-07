using LanguageExt;
using Marten;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository pour la gestion des Salons avec Event Sourcing
/// </summary>
public class SalonRepository : ISalonRepository
{
    private readonly IDocumentSession _session;

    public SalonRepository(IDocumentSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async Task<Validation<Error, Unit>> SaveAsync(
        SalonAggregate aggregate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (aggregate.Id == Guid.Empty)
                return Error.Validation("INVALID_AGGREGATE_ID", "L'identifiant de l'aggregate est vide")
                    .ToFailure<Unit>();

            // Récupérer les événements non commités
            var events = aggregate.DomainEvents.ToArray();
            if (events.Length == 0)
                return unit.ToSuccess();

            // Append les événements au stream
            _session.Events.Append(aggregate.Id, events);
            
            // Sauvegarder les changements
            await _session.SaveChangesAsync(cancellationToken);
            
            // Vider les événements non commités
            aggregate.ClearDomainEvents();

            return unit.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("SAVE_ERROR", $"Erreur lors de la sauvegarde : {ex.Message}")
                .ToFailure<Unit>();
        }
    }

    public async Task<Validation<Error, SalonAggregate>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id == Guid.Empty)
                return Error.Validation("INVALID_ID", "L'identifiant est vide")
                    .ToFailure<SalonAggregate>();

            // Recharger l'aggregate depuis le stream d'événements
            var aggregate = await _session.Events.AggregateStreamAsync<SalonAggregate>(id, token: cancellationToken);

            if (aggregate == null || aggregate.Id == Guid.Empty)
                return Error.NotFound("Salon", id)
                    .ToFailure<SalonAggregate>();

            return aggregate.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("LOAD_ERROR", $"Erreur lors du chargement : {ex.Message}")
                .ToFailure<SalonAggregate>();
        }
    }

    public async Task<IReadOnlyList<SalonAggregate>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Récupérer toutes les projections de salons non supprimés
            var projections = await _session.Query<Projections.SalonProjection>()
                .Where(p => !p.IsDeleted)
                .ToListAsync(cancellationToken);

            // Rehydrater les aggregates depuis leurs streams
            var aggregates = new List<SalonAggregate>();
            foreach (var projection in projections)
            {
                var aggregate = await _session.Events.AggregateStreamAsync<SalonAggregate>(
                    projection.Id, 
                    token: cancellationToken);
                
                if (aggregate != null && aggregate.Id != Guid.Empty)
                    aggregates.Add(aggregate);
            }

            return aggregates.AsReadOnly();
        }
        catch (Exception)
        {
            // En cas d'erreur, retourner une liste vide
            return System.Array.Empty<SalonAggregate>();
        }
    }

    public async Task<SalonAggregate?> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Récupérer la projection du salon actif
            var projection = await _session.Query<Projections.SalonProjection>()
                .Where(p => p.IsActive && !p.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (projection == null)
                return null;

            // Rehydrater l'aggregate depuis son stream
            var aggregate = await _session.Events.AggregateStreamAsync<SalonAggregate>(
                projection.Id,
                token: cancellationToken);

            return aggregate?.Id != Guid.Empty ? aggregate : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<Validation<Error, Unit>> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Charger l'aggregate
        var aggregateResult = await GetByIdAsync(id, cancellationToken);

        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        // Appeler la méthode Delete de l'aggregate
        var deleteResult = aggregate.Delete();

        if (deleteResult.IsFail)
            return deleteResult;

        // Sauvegarder l'aggregate avec l'événement de suppression
        return await SaveAsync(aggregate, cancellationToken);
    }
}
