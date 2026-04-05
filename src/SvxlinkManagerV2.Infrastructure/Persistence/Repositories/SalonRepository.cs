using LanguageExt;
using Microsoft.EntityFrameworkCore;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository pour la gestion des Salons avec EF Core
/// </summary>
public class SalonRepository : ISalonRepository
{
    private readonly SvxlinkDbContext _context;

    public SalonRepository(SvxlinkDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
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

            var existing = await _context.Salons.FindAsync(new object[] { aggregate.Id }, cancellationToken);
            if (existing == null)
                _context.Salons.Add(aggregate);
            else
            {
                _context.Entry(existing).State = EntityState.Detached;
                _context.Salons.Update(aggregate);
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

    public async Task<Validation<Error, SalonAggregate>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (id == Guid.Empty)
                return Error.Validation("INVALID_ID", "L'identifiant est vide")
                    .ToFailure<SalonAggregate>();

            var aggregate = await _context.Salons.FindAsync(new object[] { id }, cancellationToken);

            if (aggregate == null || aggregate.IsDeleted)
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
        return await _context.Salons
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<SalonAggregate?> GetDefaultAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Salons
                .Where(s => s.IsDefault && !s.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<SalonAggregate?> GetByDtmfCodeAsync(
        int dtmfCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Salons
                .Where(s => s.DtmfCode == dtmfCode && !s.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch
        {
            return null;
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
