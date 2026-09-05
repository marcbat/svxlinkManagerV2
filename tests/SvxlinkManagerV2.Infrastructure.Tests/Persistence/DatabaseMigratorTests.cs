using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Infrastructure.Persistence;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests d'intégration de la montée de version du schéma SQLite.
///
/// Le scénario critique est celui d'une installation existante : jusqu'ici le schéma
/// était créé par <c>EnsureCreated()</c>, qui ne met jamais à jour une base déjà
/// présente. Les bases déployées n'ont donc pas de table <c>__EFMigrationsHistory</c>
/// et se présentent dans trois états, tous reproduits ici : antérieure aux salons
/// Parrot, v1.0.0 (sans authentification) et post-authentification.
///
/// L'exigence est double : le schéma doit être complété, et les données utilisateur
/// (salons, SA818, configuration générale) doivent survivre à l'opération.
/// </summary>
[Trait("Category", "Integration")]
public class DatabaseMigratorTests : IDisposable
{
    /// <summary>
    /// Identifiants attendus dans <c>__EFMigrationsHistory</c> une fois la base à jour.
    ///
    /// Les trois premiers correspondent à un état de base héritée reconnu par
    /// <see cref="DatabaseMigrator.AdoptLegacyDatabase"/>, d'où leurs constantes. Les suivants
    /// n'en ont pas : aucune base créée par <c>EnsureCreated()</c> ne les reflète, elles sont
    /// simplement appliquées par <c>Migrate()</c>. Toute nouvelle migration s'ajoute ici.
    /// </summary>
    private static readonly string[] AllMigrations =
    [
        DatabaseMigrator.InitialCreateId,
        DatabaseMigrator.AddSalonTypeId,
        DatabaseMigrator.AddIdentitySchemaId,
        "20260830140047_AddAudioConfiguration",
        "20260830173529_AddActivityHistory"
    ];

    private readonly SqliteConnection _connection;

    public DatabaseMigratorTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task MigrateAsync_SurBaseNeuve_CreeLeSchemaCompletEtLHistorique()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        await DatabaseMigrator.MigrateAsync(context, NullLogger.Instance);

        // Assert
        TableExists("Salons").Should().BeTrue();
        TableExists("AspNetUsers").Should().BeTrue();
        ColumnExists("Salons", "SalonType").Should().BeTrue();
        AppliedMigrations().Should().BeEquivalentTo(AllMigrations);
    }

    [Fact]
    public async Task MigrateAsync_SurBaseV100_AjouteIdentiteEtConserveLesDonnees()
    {
        // Arrange - base v1.0.0 : SalonType present, pas d'authentification
        SeedLegacyDatabase(withSalonType: true, withIdentity: false);

        using var context = CreateContext();

        // Act
        await DatabaseMigrator.MigrateAsync(context, NullLogger.Instance);

        // Assert - le schéma est complété...
        TableExists("AspNetUsers").Should().BeTrue();
        AppliedMigrations().Should().BeEquivalentTo(AllMigrations);

        // ...et les données utilisateur sont intactes.
        await AssertUserDataPreservedAsync(expectedSalonType: SalonType.Parrot);
    }

    [Fact]
    public async Task MigrateAsync_SurBaseAnterieureAuxSalonsParrot_AjouteSalonTypeEtConserveLesDonnees()
    {
        // Arrange - base d'avril 2026 : ni colonne SalonType, ni authentification.
        // C'est le cas qui provoquait « SQLite Error 1: no such column: s.SalonType ».
        SeedLegacyDatabase(withSalonType: false, withIdentity: false);

        using var context = CreateContext();

        // Act
        await DatabaseMigrator.MigrateAsync(context, NullLogger.Instance);

        // Assert
        ColumnExists("Salons", "SalonType").Should().BeTrue();
        TableExists("AspNetUsers").Should().BeTrue();
        AppliedMigrations().Should().BeEquivalentTo(AllMigrations);

        // La colonne étant absente de la base d'origine, les salons repassent au type
        // par défaut : seule la valeur du type est perdue, jamais le salon lui-même.
        await AssertUserDataPreservedAsync(expectedSalonType: SalonType.Reflector);
    }

    [Fact]
    public async Task MigrateAsync_SurBaseDejaAJourSansHistorique_AdopteLeSchemaSansRienModifier()
    {
        // Arrange - installation post-authentification créée par EnsureCreated()
        SeedLegacyDatabase(withSalonType: true, withIdentity: true);

        using var context = CreateContext();

        // Act
        await DatabaseMigrator.MigrateAsync(context, NullLogger.Instance);

        // Assert
        AppliedMigrations().Should().BeEquivalentTo(AllMigrations);
        await AssertUserDataPreservedAsync(expectedSalonType: SalonType.Parrot);
    }

    [Fact]
    public async Task MigrateAsync_AppeleeDeuxFois_EstIdempotente()
    {
        // Arrange
        SeedLegacyDatabase(withSalonType: false, withIdentity: false);

        using var first = CreateContext();
        await DatabaseMigrator.MigrateAsync(first, NullLogger.Instance);

        // Act
        using var second = CreateContext();
        var act = async () => await DatabaseMigrator.MigrateAsync(second, NullLogger.Instance);

        // Assert
        await act.Should().NotThrowAsync();
        AppliedMigrations().Should().BeEquivalentTo(AllMigrations);
        await AssertUserDataPreservedAsync(expectedSalonType: SalonType.Reflector);
    }

    [Fact]
    public void AdoptLegacyDatabase_SurBaseDejaMigree_NeToucheAPasALHistorique()
    {
        // Arrange
        using var context = CreateContext();
        context.Database.Migrate();

        // Act
        var adopted = DatabaseMigrator.AdoptLegacyDatabase(context, NullLogger.Instance);

        // Assert
        adopted.Should().BeEmpty();
        AppliedMigrations().Should().BeEquivalentTo(AllMigrations);
    }

    private SvxlinkDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SvxlinkDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new SvxlinkDbContext(options);
    }

    /// <summary>
    /// Reconstitue une base telle que la produisait <c>EnsureCreated()</c> à une époque
    /// donnée : schéma courant, puis retrait des éléments introduits depuis, et enfin
    /// suppression de la table d'historique - que <c>EnsureCreated()</c> ne crée jamais.
    /// </summary>
    private void SeedLegacyDatabase(bool withSalonType, bool withIdentity)
    {
        using var context = CreateContext();
        context.Database.EnsureCreated();

        SeedUserData(context);

        if (!withIdentity)
        {
            foreach (var table in new[]
                     {
                         "AspNetRoleClaims", "AspNetUserRoles", "AspNetUserClaims",
                         "AspNetUserLogins", "AspNetUserTokens", "AspNetRoles", "AspNetUsers"
                     })
            {
                context.Database.ExecuteSqlRaw($"DROP TABLE IF EXISTS \"{table}\";");
            }
        }

        if (!withSalonType)
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Salons\" DROP COLUMN \"SalonType\";");

        // Aucune base héritée ne connaît les niveaux audio ni l'historique d'activité : ces tables
        // sont postérieures à l'abandon d'EnsureCreated(), c'est Migrate() qui doit les créer.
        foreach (var table in new[] { "AudioConfigurations", "ActivityEvents", "SalonSessions" })
            context.Database.ExecuteSqlRaw($"DROP TABLE IF EXISTS \"{table}\";");

        context.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS \"__EFMigrationsHistory\";");
    }

    /// <summary>
    /// Insère les données que la mise à jour doit impérativement préserver.
    /// </summary>
    private static void SeedUserData(SvxlinkDbContext context)
    {
        var reflectorSalon = SalonAggregate.Create(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Salon National France",
                isDefault: true,
                CreateValidConfiguration())
            .Match(Succ: s => s, Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));

        reflectorSalon.UpdateDtmfCode(1234)
            .Match(Succ: _ => _, Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));

        var parrotSalon = SalonAggregate.Create(
                SalonAggregate.FixedParrotId,
                "Perroquet",
                isDefault: false,
                CreateValidConfiguration(),
                salonType: SalonType.Parrot)
            .Match(Succ: s => s, Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));

        var sa818 = SA818Aggregate.Create(volume: 7, squelch: 2, bandwidth: SA818Bandwidth.Narrow12_5kHz)
            .Match(Succ: s => s, Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));

        var generalConfiguration = GeneralConfigurationAggregate.Create(
                startReflectorOnStartup: true,
                startDefaultSalonOnStartup: true,
                defaultRxFrequency: 430.325m,
                defaultTxFrequency: 430.325m)
            .Match(Succ: c => c, Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));

        context.Salons.AddRange(reflectorSalon, parrotSalon);
        context.SA818.Add(sa818);
        context.GeneralConfigurations.Add(generalConfiguration);
        context.SaveChanges();
    }

    /// <summary>
    /// Vérifie que les salons, la configuration SA818 et la configuration générale
    /// ont traversé la migration sans altération.
    /// </summary>
    private async Task AssertUserDataPreservedAsync(SalonType expectedSalonType)
    {
        using var context = CreateContext();

        var salons = await context.Salons.ToListAsync();
        salons.Should().HaveCount(2);

        var national = salons.Single(s => s.Name == "Salon National France");
        national.IsDefault.Should().BeTrue();
        national.DtmfCode.Should().Be(1234);
        national.Configuration.Host.Should().Be("ref.f5kri.fr");
        national.Configuration.Callsign.Should().Be("F5ABC-L");
        national.Configuration.AuthKey.Should().Be("test-auth-key-123");

        salons.Single(s => s.Name == "Perroquet").SalonType.Should().Be(expectedSalonType);

        var sa818 = await context.SA818.SingleAsync();
        sa818.Volume.Should().Be(7);
        sa818.Squelch.Should().Be(2);
        sa818.Bandwidth.Should().Be(SA818Bandwidth.Narrow12_5kHz);

        var generalConfiguration = await context.GeneralConfigurations.SingleAsync();
        generalConfiguration.StartReflectorOnStartup.Should().BeTrue();
        generalConfiguration.DefaultRxFrequency.Should().Be(430.325m);
    }

    private static SvxLinkConfiguration CreateValidConfiguration()
    {
        return new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d", 16000, 1,
            "ref.f5kri.fr", 5300,
            "F5ABC-L", "test-auth-key-123", 0,
            ReflectorProtocol.V2, null,
            "F5ABC", "ModuleHelp,ModuleParrot", 60, 60,
            "71.9", "fr_FR", 0,
            145.550m, 145.550m, 136.5m, 136.5m);
    }

    private List<string> AppliedMigrations()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\";";

        var ids = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            ids.Add(reader.GetString(0));

        return ids;
    }

    private bool TableExists(string name)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", name);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private bool ColumnExists(string table, string column)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $column;";
        command.Parameters.AddWithValue("$column", column);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }
}
