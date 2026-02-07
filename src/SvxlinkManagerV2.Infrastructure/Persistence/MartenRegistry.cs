using Marten;

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
        
        // TODO: Ajouter les projections ici quand elles seront créées
        // options.Projections.Add<SalonProjection>(ProjectionLifecycle.Inline);
        
        return options;
    }
}
