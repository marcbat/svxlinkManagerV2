using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Met le schéma SQLite à niveau au démarrage via les migrations EF Core.
///
/// Le projet a longtemps utilisé <c>EnsureCreated()</c>, qui ne touche jamais une base
/// existante : une installation mise à jour conservait son ancien schéma et plantait dès
/// qu'une colonne manquait (<c>no such column: s.SalonType</c>). Les migrations remplacent
/// ce mécanisme, mais elles supposent une table d'historique <c>__EFMigrationsHistory</c>
/// que les bases créées par <c>EnsureCreated()</c> n'ont pas.
///
/// <see cref="AdoptLegacyDatabase"/> comble ce trou : il inspecte le schéma réellement
/// présent et déclare comme déjà appliquées les seules migrations qui y correspondent.
/// <c>Migrate()</c> applique ensuite le reste — sans perte de données.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>Schéma antérieur au type de salon (bases créées avant le 20/04/2026).</summary>
    internal const string InitialCreateId = "20260830085650_InitialCreate";

    /// <summary>Ajout de la colonne <c>Salons.SalonType</c> (salons Parrot).</summary>
    internal const string AddSalonTypeId = "20260830085707_AddSalonType";

    /// <summary>Ajout des tables ASP.NET Identity (authentification).</summary>
    internal const string AddIdentitySchemaId = "20260830085720_AddIdentitySchema";

    /// <summary>
    /// Adopte une éventuelle base héritée puis applique les migrations en attente.
    /// </summary>
    public static async Task MigrateAsync(
        SvxlinkDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        AdoptLegacyDatabase(context, logger);

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count == 0)
        {
            logger.LogInformation("Schéma SQLite à jour, aucune migration à appliquer.");
            return;
        }

        logger.LogWarning(
            "Application de {Count} migration(s) EF Core : {Migrations}",
            pending.Count,
            string.Join(", ", pending));

        await context.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Migrations appliquées avec succès.");
    }

    /// <summary>
    /// Marque comme appliquées les migrations déjà reflétées par le schéma d'une base
    /// créée par <c>EnsureCreated()</c>, qui ne possède pas de table d'historique.
    ///
    /// Trois états de bases héritées existent dans la nature, chacun détectable :
    /// pré-Parrot (pas de colonne <c>SalonType</c>), v1.0.0 (colonne présente, pas
    /// d'authentification) et post-authentification (table <c>AspNetUsers</c> présente).
    /// </summary>
    /// <returns>Les identifiants de migrations inscrits dans l'historique.</returns>
    internal static IReadOnlyList<string> AdoptLegacyDatabase(SvxlinkDbContext context, ILogger logger)
    {
        var history = context.Database.GetService<IHistoryRepository>();

        // Base déjà pilotée par les migrations : rien à adopter.
        if (history.Exists())
            return Array.Empty<string>();

        // Base absente ou vide : Migrate() créera l'intégralité du schéma.
        if (!TableExists(context, "Salons"))
            return Array.Empty<string>();

        var adopted = new List<string> { InitialCreateId };

        if (ColumnExists(context, "Salons", "SalonType"))
            adopted.Add(AddSalonTypeId);

        if (TableExists(context, "AspNetUsers"))
            adopted.Add(AddIdentitySchemaId);

        var known = context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var unknown = adopted.Where(id => !known.Contains(id)).ToList();

        if (unknown.Count > 0)
            throw new InvalidOperationException(
                $"Migrations introuvables dans l'assembly : {string.Join(", ", unknown)}. " +
                "Les identifiants de DatabaseMigrator doivent rester alignés sur les fichiers de Migrations/.");

        logger.LogWarning(
            "Base SQLite héritée détectée (créée par EnsureCreated, sans historique de migrations) : " +
            "adoption de {Count} migration(s) déjà reflétée(s) par le schéma — {Migrations}",
            adopted.Count,
            string.Join(", ", adopted));

        var version = ProductInfo.GetVersion();

        using var transaction = context.Database.BeginTransaction();

        context.Database.ExecuteSqlRaw(history.GetCreateScript());

        foreach (var id in adopted)
            context.Database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow(id, version)));

        transaction.Commit();

        return adopted;
    }

    private static bool TableExists(SvxlinkDbContext context, string table)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = table;
        command.Parameters.Add(parameter);

        return WithOpenConnection(context, () => Convert.ToInt64(command.ExecuteScalar()) > 0);
    }

    private static bool ColumnExists(SvxlinkDbContext context, string table, string column)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        // PRAGMA n'accepte pas de paramètre : le nom de table provient exclusivement de
        // constantes du code, jamais d'une saisie utilisateur.
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $column;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$column";
        parameter.Value = column;
        command.Parameters.Add(parameter);

        return WithOpenConnection(context, () => Convert.ToInt64(command.ExecuteScalar()) > 0);
    }

    private static T WithOpenConnection<T>(SvxlinkDbContext context, Func<T> action)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;

        if (wasClosed)
            context.Database.OpenConnection();

        try
        {
            return action();
        }
        finally
        {
            if (wasClosed)
                context.Database.CloseConnection();
        }
    }
}
