using LanguageExt;
using Marten;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository pour la gestion de la configuration générale avec Event Sourcing.
/// Il n'existe qu'une seule instance (ID fixe).
/// </summary>
public class GeneralConfigurationRepository : IGeneralConfigurationRepository
{
    private readonly IDocumentSession _session;

    public GeneralConfigurationRepository(IDocumentSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async Task<Validation<Error, Unit>> SaveAsync(
        GeneralConfigurationAggregate aggregate,
        CancellationToken cancellationToken = default)
    {
        try
        {
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
            var events = await _session.Events.FetchStreamAsync(
                GeneralConfigurationAggregate.FixedId,
                token: cancellationToken);

            if (events == null || events.Count == 0)
                return null;

            return await _session.Events.AggregateStreamAsync<GeneralConfigurationAggregate>(
                GeneralConfigurationAggregate.FixedId,
                token: cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
