using Marten;
using SvxlinkManagerV2.Infrastructure.Persistence.Projections;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Configuration du schéma Marten (Event Store + Projections)
/// </summary>
public static class MartenRegistry
{
    public static StoreOptions ConfigureMartenStore(this StoreOptions options, string connectionString)
    {
        // Chaîne de connexion PostgreSQL
        options.Connection(connectionString);
        
        // Enregistrement des projections en mode Inline (synchrone)
        options.Projections.Snapshot<SoundProjection>(Marten.Events.Projections.SnapshotLifecycle.Inline);
        options.Projections.Snapshot<SalonProjection>(Marten.Events.Projections.SnapshotLifecycle.Inline);
        options.Projections.Snapshot<SA818Projection>(Marten.Events.Projections.SnapshotLifecycle.Inline);
        options.Projections.Snapshot<ReflectorProjection>(Marten.Events.Projections.SnapshotLifecycle.Inline);
        options.Projections.Snapshot<GeneralConfigurationProjection>(Marten.Events.Projections.SnapshotLifecycle.Inline);
        
        return options;
    }
}
