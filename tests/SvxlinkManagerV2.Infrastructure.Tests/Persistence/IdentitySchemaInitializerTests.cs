using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SvxlinkManagerV2.Infrastructure.Persistence;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests unitaires pour IdentitySchemaInitializer.
///
/// Le scénario critique est celui d'une installation antérieure à l'authentification :
/// la base SQLite existe déjà sans les tables ASP.NET Identity, et EnsureCreated()
/// ne les ajoute pas. L'initialiseur doit combler ce trou sans toucher aux données
/// métier existantes.
/// </summary>
public class IdentitySchemaInitializerTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public IdentitySchemaInitializerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    private SvxlinkDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SvxlinkDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new SvxlinkDbContext(options);
    }

    /// <summary>
    /// Simule une base créée avant l'introduction de l'authentification : le schéma
    /// complet est créé, puis les tables Identity sont supprimées.
    /// </summary>
    private void CreateLegacyDatabaseWithoutIdentity()
    {
        using var context = CreateContext();
        context.Database.EnsureCreated();

        foreach (var table in new[]
                 {
                     "AspNetRoleClaims", "AspNetUserRoles", "AspNetUserClaims",
                     "AspNetUserLogins", "AspNetUserTokens", "AspNetRoles", "AspNetUsers"
                 })
        {
            context.Database.ExecuteSqlRaw($"DROP TABLE IF EXISTS \"{table}\";");
        }
    }

    private bool TableExists(string name)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", name);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    [Fact]
    public void EnsureIdentityTables_OnLegacyDatabase_CreatesIdentityTables()
    {
        // Arrange
        CreateLegacyDatabaseWithoutIdentity();
        TableExists("AspNetUsers").Should().BeFalse("le scénario simule une base antérieure à l'authentification");

        using var context = CreateContext();

        // Act
        var applied = IdentitySchemaInitializer.EnsureIdentityTables(context, NullLogger.Instance);

        // Assert
        applied.Should().BeTrue();
        TableExists("AspNetUsers").Should().BeTrue();
        TableExists("AspNetRoles").Should().BeTrue();
        TableExists("AspNetUserRoles").Should().BeTrue();
        TableExists("AspNetUserClaims").Should().BeTrue();
        TableExists("AspNetUserLogins").Should().BeTrue();
        TableExists("AspNetUserTokens").Should().BeTrue();
        TableExists("AspNetRoleClaims").Should().BeTrue();
    }

    [Fact]
    public void EnsureIdentityTables_OnLegacyDatabase_PreservesExistingData()
    {
        // Arrange
        CreateLegacyDatabaseWithoutIdentity();

        using (var seedContext = CreateContext())
        {
            seedContext.Database.ExecuteSqlRaw(
                "INSERT INTO \"Salons\" (\"Id\", \"Name\", \"IsDefault\", \"IsDeleted\", \"DtmfCode\", \"SalonType\", \"Configuration\") " +
                "VALUES ({0}, {1}, 0, 0, 42, 0, {2});",
                "11111111-1111-1111-1111-111111111111",
                "Salon existant",
                "{}");
        }

        using var context = CreateContext();

        // Act
        IdentitySchemaInitializer.EnsureIdentityTables(context, NullLogger.Instance);

        // Assert
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"Salons\" WHERE \"Name\" = 'Salon existant';";
        Convert.ToInt64(command.ExecuteScalar()).Should().Be(1);
    }

    [Fact]
    public void EnsureIdentityTables_WhenTablesAlreadyExist_DoesNothing()
    {
        // Arrange - EnsureCreated() sur une base neuve crée déjà les tables Identity
        using var context = CreateContext();
        context.Database.EnsureCreated();

        // Act
        var applied = IdentitySchemaInitializer.EnsureIdentityTables(context, NullLogger.Instance);

        // Assert
        applied.Should().BeFalse();
    }

    [Fact]
    public void EnsureIdentityTables_IsIdempotent()
    {
        // Arrange
        CreateLegacyDatabaseWithoutIdentity();
        using var context = CreateContext();
        IdentitySchemaInitializer.EnsureIdentityTables(context, NullLogger.Instance);

        // Act - un second appel ne doit ni échouer ni recréer quoi que ce soit
        var applied = IdentitySchemaInitializer.EnsureIdentityTables(context, NullLogger.Instance);

        // Assert
        applied.Should().BeFalse();
        TableExists("AspNetUsers").Should().BeTrue();
    }

    [Fact]
    public void ExtractIdentityStatements_KeepsOnlyIdentityObjects()
    {
        // Arrange
        using var context = CreateContext();
        var script = context.Database.GenerateCreateScript();

        // Act
        var statements = IdentitySchemaInitializer.ExtractIdentityStatements(script);

        // Assert
        statements.Should().NotBeEmpty();
        statements.Should().OnlyContain(s => s.Contains("\"AspNet", StringComparison.Ordinal));
        statements.Should().OnlyContain(s => s.Contains("IF NOT EXISTS", StringComparison.Ordinal));
        statements.Should().NotContain(s => s.Contains("\"Salons\"", StringComparison.Ordinal));
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
