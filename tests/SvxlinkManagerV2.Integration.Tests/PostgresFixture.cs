using Marten;
using SvxlinkManagerV2.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// Collection xUnit pour partager la fixture PostgreSQL entre TOUS les tests d'intégration.
/// Garantit qu'un seul container PostgreSQL est créé pour toute la suite de tests.
/// </summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestsCollection : ICollectionFixture<PostgresFixture>
{
}

/// <summary>
/// Fixture pour créer et gérer un conteneur PostgreSQL avec Testcontainers.
/// Partagée entre tous les tests d'intégration Application ↔ Infrastructure.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    /// <summary>
    /// DocumentStore Marten configuré avec Event Sourcing et projections
    /// </summary>
    public IDocumentStore DocumentStore { get; private set; } = null!;

    /// <summary>
    /// Chaîne de connexion PostgreSQL du conteneur
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Démarre le conteneur PostgreSQL et configure Marten
    /// </summary>
    public async Task InitializeAsync()
    {
        // Créer et démarrer le conteneur PostgreSQL
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("svxlink_integration_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();

        // Récupérer la chaîne de connexion
        ConnectionString = _container.GetConnectionString();

        // Configurer Marten avec Event Sourcing en utilisant l'extension du projet
        DocumentStore = Marten.DocumentStore.For(options =>
        {
            options.ConfigureMartenStore(ConnectionString);
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
