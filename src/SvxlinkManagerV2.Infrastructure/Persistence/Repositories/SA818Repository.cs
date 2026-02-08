using LanguageExt;
using Marten;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Infrastructure.Persistence.Projections;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository pour la gestion du SA818 avec Event Sourcing.
/// Le SA818 possède un ID fixe (un seul device physique).
/// </summary>
public class SA818Repository : ISA818Repository
{
    private readonly IDocumentSession _session;

    public SA818Repository(IDocumentSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
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

            // Récupérer les événements non commités
            var events = aggregate.DomainEvents.ToArray();
            if (events.Length == 0)
                return unit.ToSuccess();

            // Append les événements au stream (utilise l'ID fixe)
            _session.Events.Append(aggregate.Id, events);
            
            // Sauvegarder les changements
            await _session.SaveChangesAsync(cancellationToken);
            
            // Vider les événements non commités
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
            // Recharger l'aggregate depuis le stream d'événements (ID fixe)
            var aggregate = await _session.Events.AggregateStreamAsync<SA818Aggregate>(
                SA818Aggregate.FixedId, 
                token: cancellationToken);

            if (aggregate == null || aggregate.Id == Guid.Empty)
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
            // Récupérer la projection unique du SA818 (ID fixe)
            var projection = await _session.Query<SA818Projection>()
                .Where(p => p.Id == SA818Aggregate.FixedId)
                .SingleOrDefaultAsync(cancellationToken);

            if (projection == null)
                return null;

            // Mapper la projection vers le DTO
            return new SA818ConfigurationDto
            {
                Id = projection.Id,
                Volume = projection.Volume,
                Squelch = projection.Squelch,
                Bandwidth = projection.Bandwidth,
                PreEmph = projection.PreEmph,
                HighPass = projection.HighPass,
                LowPass = projection.LowPass,
                UpdatedAt = projection.UpdatedAt
            };
        }
        catch
        {
            // En cas d'erreur, retourner null plutôt que de propager l'exception
            // Le SA818 sera considéré comme non initialisé
            return null;
        }
    }

    public async Task<SA818Projection?> GetProjectionAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Récupérer la projection unique du SA818 (ID fixe)
            var projection = await _session.Query<SA818Projection>()
                .Where(p => p.Id == SA818Aggregate.FixedId)
                .SingleOrDefaultAsync(cancellationToken);

            return projection;
        }
        catch
        {
            // En cas d'erreur, retourner null plutôt que de propager l'exception
            // Le SA818 sera considéré comme non initialisé
            return null;
        }
    }
}
