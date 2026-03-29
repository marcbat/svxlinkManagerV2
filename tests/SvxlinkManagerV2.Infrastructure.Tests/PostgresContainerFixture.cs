using Microsoft.EntityFrameworkCore;
using SvxlinkManagerV2.Infrastructure.Persistence;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests;

/// <summary>
/// Collection xUnit pour partager la fixture SQLite entre TOUS les tests d'infrastructure.
/// </summary>
[CollectionDefinition("PostgresIntegration")]
public class PostgresIntegrationCollection : ICollectionFixture<PostgresContainerFixture>
{
}

/// <summary>
/// Fixture SQLite pour les tests d'infrastructure.
/// Remplace le conteneur PostgreSQL par SQLite en mémoire.
/// </summary>
public class PostgresContainerFixture : IAsyncLifetime
{
    public SvxlinkDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SvxlinkDbContext>()
            .UseSqlite($"Data Source={Guid.NewGuid():N}.db")
            .Options;
        var context = new SvxlinkDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
}
