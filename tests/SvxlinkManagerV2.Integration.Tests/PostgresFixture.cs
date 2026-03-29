using Microsoft.EntityFrameworkCore;
using SvxlinkManagerV2.Infrastructure.Persistence;
using Xunit;

namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// Collection xUnit pour partager la fixture SQLite entre TOUS les tests d'intégration.
/// </summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestsCollection : ICollectionFixture<SqliteFixture>
{
}

/// <summary>
/// Fixture SQLite pour les tests d'intégration.
/// Chaque appel à CreateDbContext crée une base de données SQLite en mémoire unique.
/// </summary>
public class SqliteFixture : IAsyncLifetime
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
