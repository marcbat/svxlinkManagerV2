using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Infrastructure.Hardware;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using SvxlinkManagerV2.Presentation.Data;

namespace SvxlinkManagerV2.Presentation
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Configuration Marten avec Event Sourcing
            services.AddMarten(options =>
            {
                var connectionString = Configuration.GetConnectionString("PostgreSQL") 
                    ?? throw new InvalidOperationException("ConnectionString PostgreSQL manquante");
                    
                options.ConfigureMartenStore(connectionString);
            })
            .UseLightweightSessions();
            
            // Enregistrement du repository Event Store
            services.AddScoped<IEventStoreRepository, MartenEventStoreRepository>();
            
            // Enregistrement des repositories
            services.AddScoped<ISA818Repository, SA818Repository>();
            services.AddScoped<ISalonRepository, SalonRepository>();
            services.AddScoped<ISoundRepository, SoundRepository>();
            
            // Enregistrement du service d'initialisation SA818 (s'exécute au démarrage)
            services.AddHostedService<SA818InitializerHostedService>();
            
            // Enregistrement conditionnel du service SA818 (réel ou mock selon configuration)
            var useSA818Mock = Configuration.GetValue<bool>("SA818:UseMock", false);
            if (useSA818Mock)
            {
                services.AddScoped<ISA818Service, SA818MockService>();
            }
            else
            {
                services.AddScoped<ISA818Service, SA818Service>();
            }
            
            // Enregistrement du service daemon SVXLink (toujours l'implémentation réelle)
            // En DEV: exécuté dans le container Docker avec SVXLink installé
            // En PROD: exécuté sur Orange Pi avec Armbian et SVXLink installé
            services.AddScoped<ISvxLinkDaemonService, SvxLinkDaemonService>();
            
            // Enregistrement du service de génération de configuration SVXLink (toujours réel)
            services.AddScoped<ISvxLinkConfigurationService, SvxLinkConfigurationService>();
            
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddSingleton<WeatherForecastService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
