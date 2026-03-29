using LanguageExt;
using Microsoft.EntityFrameworkCore;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository pour la gestion de la configuration générale avec EF Core.
/// Il n'existe qu'une seule instance (ID fixe).
/// </summary>
public class GeneralConfigurationRepository : IGeneralConfigurationRepository
{
    private readonly SvxlinkDbContext _context;

    public GeneralConfigurationRepository(SvxlinkDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Validation<Error, Unit>> SaveAsync(
        GeneralConfigurationAggregate aggregate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _context.GeneralConfigurations
                .FindAsync(new object[] { aggregate.Id }, cancellationToken);

            if (existing == null)
                _context.GeneralConfigurations.Add(aggregate);
            else
            {
                _context.Entry(existing).State = EntityState.Detached;
                _context.GeneralConfigurations.Update(aggregate);
            }
            await _context.SaveChangesAsync(cancellationToken);
            aggregate.ClearDomainEvents();
            return unit.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("SAVE_ERROR",
                $"Erreur lors de la sauvegarde de la configuration générale : {ex.Message}")
                .ToFailure<Unit>();
        }
    }

    public async Task<GeneralConfigurationAggregate?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.GeneralConfigurations
                .FindAsync(new object[] { GeneralConfigurationAggregate.FixedId }, cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
