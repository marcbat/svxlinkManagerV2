using LanguageExt;
using Microsoft.EntityFrameworkCore;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository EF Core des niveaux ALSA mémorisés.
/// La configuration audio possède un ID fixe (une seule carte son pilotée).
/// </summary>
public class AudioConfigurationRepository : IAudioConfigurationRepository
{
    private readonly SvxlinkDbContext _context;

    public AudioConfigurationRepository(SvxlinkDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Validation<Error, AudioConfigurationAggregate>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var aggregate = await _context.AudioConfigurations
                .FindAsync(new object[] { AudioConfigurationAggregate.FixedId }, cancellationToken);

            if (aggregate == null)
                return Error.NotFound("AudioConfiguration", AudioConfigurationAggregate.FixedId)
                    .ToFailure<AudioConfigurationAggregate>();

            return aggregate.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("LOAD_ERROR", $"Erreur lors du chargement de la configuration audio : {ex.Message}")
                .ToFailure<AudioConfigurationAggregate>();
        }
    }

    public async Task<Validation<Error, Unit>> SaveAsync(
        AudioConfigurationAggregate aggregate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (aggregate.Id != AudioConfigurationAggregate.FixedId)
                return Error.Validation("INVALID_AGGREGATE_ID",
                        $"L'identifiant de la configuration audio doit être {AudioConfigurationAggregate.FixedId}")
                    .ToFailure<Unit>();

            var existing = await _context.AudioConfigurations
                .FindAsync(new object[] { aggregate.Id }, cancellationToken);

            if (existing == null)
                _context.AudioConfigurations.Add(aggregate);
            else if (!ReferenceEquals(existing, aggregate))
            {
                _context.Entry(existing).State = EntityState.Detached;
                _context.AudioConfigurations.Update(aggregate);
            }

            await _context.SaveChangesAsync(cancellationToken);
            aggregate.ClearDomainEvents();

            return unit.ToSuccess();
        }
        catch (Exception ex)
        {
            return Error.Validation("SAVE_ERROR", $"Erreur lors de la sauvegarde de la configuration audio : {ex.Message}")
                .ToFailure<Unit>();
        }
    }
}
