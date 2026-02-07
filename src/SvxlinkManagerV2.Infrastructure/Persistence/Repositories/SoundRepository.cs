using LanguageExt;
using Marten;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository pour la gestion des Sound avec Event Sourcing
/// </summary>
public class SoundRepository : ISoundRepository
{
    private readonly IDocumentSession _session;

    public SoundRepository(IDocumentSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async Task<Validation<Error, Unit>> SaveAsync(
        SoundAggregate aggregate,
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

    public async Task<Validation<Error, SoundAggregate>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id == Guid.Empty)
                return Error.Validation("INVALID_ID", "L'identifiant est vide")
                    .ToFailure<SoundAggregate>();

            // Recharger l'aggregate depuis le stream d'événements
            var aggregate = await _session.Events.AggregateStreamAsync<SoundAggregate>(id, token: cancellationToken);

            if (aggregate == null || aggregate.Id == Guid.Empty)
                return Error.NotFound("Sound", id)
                    .ToFailure<SoundAggregate>();

            return aggregate.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("LOAD_ERROR", $"Erreur lors du chargement : {ex.Message}")
                .ToFailure<SoundAggregate>();
        }
    }

    public async Task<IReadOnlyList<SoundAggregate>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Récupérer toutes les projections
            var projections = await _session
                .Query<Projections.SoundProjection>()
                .Where(p => !p.IsDeleted)
                .ToListAsync(token: cancellationToken);

            // Recharger chaque aggregate depuis son stream
            var aggregates = new List<SoundAggregate>();
            foreach (var projection in projections)
            {
                var aggregate = await _session.Events.AggregateStreamAsync<SoundAggregate>(
                    projection.Id,
                    token: cancellationToken);

                if (aggregate != null && !aggregate.IsDeleted)
                    aggregates.Add(aggregate);
            }

            return aggregates.AsReadOnly();
        }
        catch
        {
            return new List<SoundAggregate>().AsReadOnly();
        }
    }

    public async Task<Validation<Error, Unit>> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
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

            // Supprimer logiquement
            var deleteResult = aggregate.Delete();

            if (deleteResult.IsFail)
                return deleteResult;

            // Sauvegarder
            return await SaveAsync(aggregate, cancellationToken);
        }
        catch (Exception ex)
        {
            return Error.Validation("DELETE_ERROR", $"Erreur lors de la suppression : {ex.Message}")
                .ToFailure<Unit>();
        }
    }
}
