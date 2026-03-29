using LanguageExt;
using Microsoft.EntityFrameworkCore;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository pour la gestion du SA818 avec EF Core.
/// Le SA818 possède un ID fixe (un seul device physique).
/// </summary>
public class SA818Repository : ISA818Repository
{
    private readonly SvxlinkDbContext _context;

    public SA818Repository(SvxlinkDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Validation<Error, Unit>> SaveAsync(
        SA818Aggregate aggregate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (aggregate.Id != SA818Aggregate.FixedId)
                return Error.Validation("INVALID_AGGREGATE_ID",
                    $"L'identifiant du SA818 doit être {SA818Aggregate.FixedId}")
                    .ToFailure<Unit>();

            var existing = await _context.SA818.FindAsync(new object[] { aggregate.Id }, cancellationToken);
            if (existing == null)
                _context.SA818.Add(aggregate);
            else
            {
                _context.Entry(existing).State = EntityState.Detached;
                _context.SA818.Update(aggregate);
            }
            await _context.SaveChangesAsync(cancellationToken);
            aggregate.ClearDomainEvents();
            return unit.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("SAVE_ERROR", $"Erreur lors de la sauvegarde du SA818 : {ex.Message}")
                .ToFailure<Unit>();
        }
    }

    public async Task<Validation<Error, SA818Aggregate>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var aggregate = await _context.SA818.FindAsync(new object[] { SA818Aggregate.FixedId }, cancellationToken);
            if (aggregate == null)
                return Error.NotFound("SA818", SA818Aggregate.FixedId)
                    .ToFailure<SA818Aggregate>();
            return aggregate.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("LOAD_ERROR", $"Erreur lors du chargement du SA818 : {ex.Message}")
                .ToFailure<SA818Aggregate>();
        }
    }

    public async Task<SA818ConfigurationDto?> GetConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var aggregate = await _context.SA818.FindAsync(new object[] { SA818Aggregate.FixedId }, cancellationToken);
            if (aggregate == null)
                return null;

            return new SA818ConfigurationDto
            {
                Id = aggregate.Id,
                Volume = aggregate.Volume,
                Squelch = aggregate.Squelch,
                Bandwidth = aggregate.Bandwidth,
                PreEmph = aggregate.PreEmph,
                HighPass = aggregate.HighPass,
                LowPass = aggregate.LowPass,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }
}
