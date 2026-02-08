using Marten;
using Marten.Events.Projections;
using Testcontainers.PostgreSql;
using Weasel.Core;
using Xunit;
using SvxlinkManagerV2.Infrastructure.Persistence;

namespace SvxlinkManagerV2.Infrastructure.Tests;

/// <summary>
/// Collection xUnit pour partager la fixture PostgreSQL entre TOUS les tests d'intégration.
/// Cela garantit qu'un seul container PostgreSQL est créé pour toute la suite de tests.
/// </summary>
[CollectionDefinition("PostgresIntegration")]
public class PostgresIntegrationCollection : ICollectionFixture<PostgresContainerFixture>
{
}

/// <summary>
/// Fixture pour créer et gérer un conteneur PostgreSQL avec Testcontainers.
/// Partagée entre tous les tests via la collection "PostgresIntegration".
/// </summary>
public class PostgresContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    /// <summary>
    /// DocumentStore Marten configuré avec Event Sourcing et projections
    /// </summary>
    public IDocumentStore DocumentStore { get; private set; } = null!;

    /// <summary>
    /// Démarre le conteneur PostgreSQL et configure Marten
    /// </summary>
    public async Task InitializeAsync()
    {
        // Créer et démarrer le conteneur PostgreSQL
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("svxlink_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();

        // Configurer Marten avec Event Sourcing en utilisant l'extension du projet
        DocumentStore = Marten.DocumentStore.For(options =>
        {
            options.ConfigureMartenStore(_container.GetConnectionString());
        });

        // Initialiser le schéma de base de données
        await DocumentStore.Advanced.Clean.CompletelyRemoveAllAsync();
    }

    /// <summary>
    /// Arrête et nettoie le conteneur PostgreSQL
    /// </summary>
    public async Task DisposeAsync()
    {
        DocumentStore?.Dispose();
        
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
