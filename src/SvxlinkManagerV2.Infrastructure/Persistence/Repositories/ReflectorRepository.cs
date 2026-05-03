using LanguageExt;
using Microsoft.EntityFrameworkCore;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository pour la gestion du Reflector avec EF Core
/// </summary>
public class ReflectorRepository : IReflectorRepository
{
    private readonly SvxlinkDbContext _context;

    public ReflectorRepository(SvxlinkDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
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

            var existing = await _context.Reflectors.FindAsync(new object[] { aggregate.Id }, cancellationToken);
            if (existing == null)
                _context.Reflectors.Add(aggregate);
            else
            {
                _context.Entry(existing).State = EntityState.Detached;
                _context.Reflectors.Update(aggregate);
            }
            await _context.SaveChangesAsync(cancellationToken);
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

            var aggregate = await _context.Reflectors.FindAsync(new object[] { id }, cancellationToken);

            if (aggregate == null || aggregate.IsDeleted)
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
        return await _context.Reflectors
            .Where(r => !r.IsDeleted)
            .ToListAsync(cancellationToken);
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
