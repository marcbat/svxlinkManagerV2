using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Wolverine;

namespace SvxlinkManagerV2.Presentation
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await CreateHostBuilder(args).RunJasperFxCommands(args);
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWolverine(opts =>
                {
                    // Découverte automatique des handlers dans l'assembly Application
                    opts.Discovery.IncludeAssembly(typeof(SvxlinkManagerV2.Application.Features.Ping.PingCommand).Assembly);

                    // En Production : utiliser les handlers pré-compilés (Static mode)
                    // Évite le chargement de Roslyn (~150MB RAM) sur Orange Pi 512MB
                    // Pré-requis : exécuter 'dotnet run -- codegen write' avant chaque publish
                    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
                    if (environment == "Production")
                    {
                        opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
                    }
                })
                .ConfigureLogging(logging =>
                {
                    // Format single-line avec timestamp — lisible dans docker logs
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
