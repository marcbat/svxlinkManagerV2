using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWolverine(opts =>
                {
                    // Découverte automatique des handlers dans l'assembly Application
                    opts.Discovery.IncludeAssembly(typeof(SvxlinkManagerV2.Application.Features.Ping.PingCommand).Assembly);
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
