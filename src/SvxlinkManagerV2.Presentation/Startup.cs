using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marten;
using Microsoft.AspNetCore.Builder;
using Wolverine.Marten;
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
using SvxlinkManagerV2.Infrastructure.Reflector;
using SvxlinkManagerV2.Infrastructure.Runtime;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using SvxlinkManagerV2.Presentation.Services;

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
            // IntegrateWithWolverine() : permet à Marten de publier les domain events vers le bus Wolverine
            // UseFastEventForwarding : forward automatiquement les events Marten vers les handlers Wolverine
            services.AddMarten(options =>
            {
                var connectionString = Configuration.GetConnectionString("PostgreSQL") 
                    ?? throw new InvalidOperationException("ConnectionString PostgreSQL manquante");
                    
                options.ConfigureMartenStore(connectionString);
            })
            .UseLightweightSessions()
            .IntegrateWithWolverine(x => x.UseFastEventForwarding = true);
            
            // Tracker d'état actif (runtime, singleton — réinitialisé à chaque démarrage)
            services.AddSingleton<IActiveSessionTracker, ActiveSessionTracker>();

            // Enregistrement du repository Event Store
            services.AddScoped<IEventStoreRepository, MartenEventStoreRepository>();
            
            // Enregistrement des repositories
            services.AddScoped<ISA818Repository, SA818Repository>();
            services.AddScoped<ISalonRepository, SalonRepository>();
            services.AddScoped<ISoundRepository, SoundRepository>();
            services.AddScoped<IGeneralConfigurationRepository, GeneralConfigurationRepository>();
            
            // Enregistrement du service d'initialisation SA818 (s'exécute au démarrage)
            services.AddHostedService<SA818InitializerHostedService>();

            // Activation automatique au démarrage selon la configuration générale
            services.AddHostedService<StartupActivationHostedService>();
            
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
            
            // Enregistrement du buffer de logs SVXLink en SINGLETON
            // Doit être enregistré AVANT SvxLinkDaemonService car il en dépend.
            services.AddSingleton<ISvxLinkLogService, SvxLinkLogBuffer>();
            
            // Enregistrement du tracker de nœuds connectés en SINGLETON
            // Singleton car dépend de ISvxLinkLogService (singleton) et l'état doit persister
            services.AddSingleton<IConnectedNodesService, ConnectedNodesTracker>();
            
            // Enregistrement du service daemon SVXLink en SINGLETON
            // IMPORTANT: doit être Singleton pour que le processus svxlink survive entre les requêtes.
            // Un scope Scoped causerait le kill du processus à chaque fin de handler Wolverine.
            services.AddSingleton<ISvxLinkDaemonService, SvxLinkDaemonService>();
            
            // Enregistrement du service de génération de configuration SVXLink (toujours réel)
            services.AddScoped<ISvxLinkConfigurationService, SvxLinkConfigurationService>();

            // Enregistrement des services Reflector
            // IReflectorLogService doit être enregistré AVANT IReflectorDaemonService car il en dépend.
            services.AddSingleton<IReflectorLogService, ReflectorLogBuffer>();
            services.AddSingleton<IReflectorDaemonService, ReflectorDaemonService>();
            services.AddScoped<IReflectorRepository, ReflectorRepository>();
            services.AddScoped<IReflectorConfigurationService, ReflectorConfigurationService>();

            // Diagnostics SVXLink au démarrage : banner avec mode daemon, config, état
            services.AddHostedService<SvxLinkDiagnosticsHostedService>();
            
            // Enregistrement du service Toast pour les notifications UI (singleton)
            services.AddSingleton<ToastService>();
            
            // Enregistrement du service Audio pour le formatage des métadonnées audio
            services.AddScoped<AudioService>();
            
            services.AddRazorPages();
            services.AddServerSideBlazor();
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
