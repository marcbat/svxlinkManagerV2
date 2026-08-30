using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using SvxlinkManagerV2.Infrastructure.Persistence;

namespace SvxlinkManagerV2.Presentation
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            // Mettre le schéma SQLite à niveau et journaliser les informations de démarrage critique
            using (var scope = host.Services.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

                var connectionString = configuration.GetConnectionString("SQLite") ?? "Data Source=svxlinkmanager.db";

                // Résolution du chemin absolu du fichier SQLite pour le diagnostic
                string resolvedDbPath;
                try
                {
                    var dataSourceValue = connectionString
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                        ?.Substring("Data Source=".Length)
                        ?? connectionString;

                    resolvedDbPath = Path.IsPathRooted(dataSourceValue)
                        ? dataSourceValue
                        : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, dataSourceValue));
                }
                catch
                {
                    resolvedDbPath = "(impossible de résoudre le chemin)";
                }

                logger.LogInformation(
                    "Démarrage SvxlinkManagerV2 — Environnement: {Environment} | Répertoire de travail: {WorkingDir} | Chemin base SQLite: {DbPath}",
                    environment.EnvironmentName,
                    Directory.GetCurrentDirectory(),
                    resolvedDbPath);

                logger.LogInformation("Fichier SQLite existant avant migration: {DbFileExisted}", File.Exists(resolvedDbPath));

                // Le schéma est piloté par les migrations EF Core : une base existante est
                // mise à niveau sans perte, une base absente est créée de bout en bout.
                var context = scope.ServiceProvider.GetRequiredService<SvxlinkDbContext>();
                await DatabaseMigrator.MigrateAsync(context, logger);
            }

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSimpleConsole(options =>
                    {
                        options.SingleLine = true;
                        options.TimestampFormat = "HH:mm:ss ";
                        options.IncludeScopes = false;
                        options.ColorBehavior = LoggerColorBehavior.Disabled;
                    });
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
