using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Crée les tables ASP.NET Identity sur une base SQLite préexistante.
///
/// Le projet utilise <c>EnsureCreated()</c> et non les migrations EF Core :
/// sur une installation déjà déployée, la base existe donc déjà et
/// <c>EnsureCreated()</c> ne fait rien — les tables <c>AspNet*</c> introduites par
/// <see cref="SvxlinkDbContext"/> (devenu <c>IdentityDbContext</c>) ne seraient
/// jamais créées et l'authentification planterait au premier accès.
///
/// Cette classe comble ce trou en rejouant, sur une base existante, uniquement
/// les instructions DDL du schéma courant qui concernent les tables Identity.
/// Le script est généré à partir du modèle EF Core : il reste automatiquement
/// aligné si le modèle Identity évolue.
/// </summary>
public static class IdentitySchemaInitializer
{
    /// <summary>
    /// Préfixe des tables générées par ASP.NET Identity.
    /// </summary>
    private const string IdentityTablePrefix = "AspNet";

    /// <summary>
    /// Crée les tables Identity manquantes. Sans effet si elles existent déjà.
    /// </summary>
    /// <returns><c>true</c> si au moins une instruction DDL a été exécutée.</returns>
    public static bool EnsureIdentityTables(SvxlinkDbContext context, ILogger logger)
    {
        if (IdentityTablesExist(context))
        {
            logger.LogInformation("Tables ASP.NET Identity déjà présentes, aucune action effectuée.");
            return false;
        }

        var statements = ExtractIdentityStatements(context.Database.GenerateCreateScript());

        if (statements.Count == 0)
        {
            logger.LogWarning(
                "Aucune instruction DDL Identity extraite du script de création — les tables ASP.NET Identity restent absentes.");
            return false;
        }

        logger.LogWarning(
            "Base SQLite préexistante sans tables ASP.NET Identity : création de {Count} objet(s) de schéma.",
            statements.Count);

        foreach (var statement in statements)
            context.Database.ExecuteSqlRaw(statement);

        logger.LogInformation("Tables ASP.NET Identity créées avec succès.");
        return true;
    }

    /// <summary>
    /// Vérifie la présence de la table <c>AspNetUsers</c>, témoin du schéma Identity.
    /// </summary>
    private static bool IdentityTablesExist(SvxlinkDbContext context)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AspNetUsers';";

        var wasClosed = context.Database.GetDbConnection().State != System.Data.ConnectionState.Open;
        if (wasClosed)
            context.Database.OpenConnection();

        try
        {
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }
        finally
        {
            if (wasClosed)
                context.Database.CloseConnection();
        }
    }

    /// <summary>
    /// Ne conserve du script de création complet que les instructions portant sur
    /// les tables Identity, rendues idempotentes par <c>IF NOT EXISTS</c>.
    /// </summary>
    internal static List<string> ExtractIdentityStatements(string createScript)
    {
        var statements = new List<string>();

        foreach (var raw in createScript.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var statement = raw.Trim();

            if (statement.Length == 0)
                continue;

            if (!statement.Contains($"\"{IdentityTablePrefix}", StringComparison.Ordinal))
                continue;

            if (statement.StartsWith("CREATE TABLE ", StringComparison.OrdinalIgnoreCase))
                statement = "CREATE TABLE IF NOT EXISTS " + statement["CREATE TABLE ".Length..];
            else if (statement.StartsWith("CREATE UNIQUE INDEX ", StringComparison.OrdinalIgnoreCase))
                statement = "CREATE UNIQUE INDEX IF NOT EXISTS " + statement["CREATE UNIQUE INDEX ".Length..];
            else if (statement.StartsWith("CREATE INDEX ", StringComparison.OrdinalIgnoreCase))
                statement = "CREATE INDEX IF NOT EXISTS " + statement["CREATE INDEX ".Length..];
            else
                continue;

            statements.Add(statement);
        }

        return statements;
    }
}
