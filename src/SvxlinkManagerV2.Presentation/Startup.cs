using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Application.Features.SystemStatus;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Infrastructure.Hardware;
using SvxlinkManagerV2.Infrastructure.Monitoring;
using SvxlinkManagerV2.Infrastructure.Network;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using SvxlinkManagerV2.Infrastructure.Reflector;
using SvxlinkManagerV2.Infrastructure.Runtime;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;
using SvxlinkManagerV2.Infrastructure.SvxLink.Strategies;
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

        public void ConfigureServices(IServiceCollection services)
        {
            // EF Core SQLite
            var connectionString = Configuration.GetConnectionString("SQLite") ?? "Data Source=svxlinkmanager.db";
            services.AddDbContext<SvxlinkDbContext>(options =>
                options.UseSqlite(connectionString));

            // MediatR - découverte auto dans l'assembly Application
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(SvxlinkManagerV2.Application.Features.Ping.PingCommand).Assembly));

            // Tracker d'état actif (runtime, singleton)
            services.AddSingleton<IActiveSessionTracker, ActiveSessionTracker>();

            // Détection du premier lancement (wizard de configuration)
            services.AddSingleton<ISetupStatusService, SetupStatusService>();

            // Enregistrement des repositories
            services.AddScoped<ISA818Repository, SA818Repository>();
            services.AddScoped<ISalonRepository, SalonRepository>();
            services.AddScoped<IGeneralConfigurationRepository, GeneralConfigurationRepository>();
            services.AddScoped<IReflectorRepository, ReflectorRepository>();

            // SA818 initializer
            services.AddHostedService<SA818InitializerHostedService>();

            // Seeding des salons originaux
            services.AddHostedService<SalonSeederHostedService>();

            // Seeding du réflecteur local par défaut
            services.AddHostedService<ReflectorSeederHostedService>();

            // Activation automatique au démarrage
            services.AddHostedService<StartupActivationHostedService>();

            // SA818 service (réel ou mock)
            var useSA818Mock = Configuration.GetValue<bool>("SA818:UseMock", false);
            if (useSA818Mock)
                services.AddScoped<ISA818Service, SA818MockService>();
            else
                services.AddScoped<ISA818Service, SA818Service>();

            // WiFi service (réel ou mock)
            var useWifiMock = Configuration.GetValue<bool>("Wifi:UseMock", false);
            if (useWifiMock)
                services.AddScoped<IWifiService, WifiMockService>();
            else
                services.AddScoped<IWifiService, WifiService>();

            services.Configure<ApplicationUpdateOptions>(Configuration.GetSection(ApplicationUpdateOptions.SectionName));
            services.AddHttpClient<IApplicationUpdateService, GitHubReleaseUpdateService>();
            services.AddSingleton<IApplicationUpdateWorkflowService, ApplicationUpdateWorkflowService>();

            // SVXLink version strategies (dual install: 19.09.2 legacy + 25.05 modern)
            services.AddSingleton<ISvxLinkVersionStrategy, SvxLinkLegacyStrategy>();
            services.AddSingleton<ISvxLinkVersionStrategy, SvxLinkModernStrategy>();
            services.AddSingleton<ISvxLinkStrategyResolver, SvxLinkStrategyResolver>();

            // SVXLink services
            services.AddSingleton<ISvxLinkLogService, SvxLinkLogBuffer>();
            services.AddSingleton<IConnectedNodesService, ConnectedNodesTracker>();
            services.AddSingleton<IDtmfCommandTracker, DtmfCommandTracker>();
            services.AddSingleton<ISvxLinkDaemonService, SvxLinkDaemonService>();
            services.AddScoped<ISvxLinkConfigurationService, SvxLinkConfigurationService>();
            services.AddScoped<ISalonAnnouncementService, SalonAnnouncementService>();
            services.AddScoped<ILogicTclDeploymentService, LogicTclDeploymentService>();
            services.AddHostedService<LogicTclInitializerHostedService>();
            services.AddHostedService<DtmfSalonSwitchService>();
            services.AddHostedService<DtmfAnnounceService>();
            services.AddHostedService<DtmfSystemCommandService>();
            services.AddHostedService<ReflectorConnectionAnnouncementService>();

            // Supervision système (page Système + annonces DTMF)
            services.Configure<SystemMonitoringOptions>(Configuration.GetSection(SystemMonitoringOptions.SectionName));
            services.AddSingleton<ISystemMetricsService, LinuxSystemMetricsService>();

            // TTS et providers d'information pour les commandes DTMF 301-398
            services.AddSingleton<ITtsService, PicoTtsService>();
            services.AddSingleton<IDtmfPtyWriter, DtmfPtyWriter>();
            services.AddSingleton<IVoiceAnnouncementService, VoiceAnnouncementService>();
            services.AddSingleton<IInfoProvider, CpuTemperatureInfoProvider>();
            services.AddSingleton<IInfoProvider, IpAddressInfoProvider>();
            services.AddSingleton<IInfoProvider, NetworkStatusInfoProvider>();
            services.AddSingleton<IInfoProvider, DiskSpaceInfoProvider>();
            services.AddSingleton<IInfoProvider, UptimeInfoProvider>();
            services.AddSingleton<IInfoProvider, CpuLoadInfoProvider>();
            services.AddSingleton<IInfoProvider, MemoryInfoProvider>();

            // Reflector services
            services.AddSingleton<IReflectorLogService, ReflectorLogBuffer>();
            services.AddSingleton<IReflectorDaemonService, ReflectorDaemonService>();
            services.AddScoped<IReflectorConfigurationService, ReflectorConfigurationService>();

            // Contrôle d'alimentation de la machine (redémarrage / arrêt)
            services.Configure<SystemControlOptions>(Configuration.GetSection(SystemControlOptions.SectionName));
            var useSystemControlMock = Configuration.GetValue<bool>($"{SystemControlOptions.SectionName}:UseMock", false);
            if (useSystemControlMock)
                services.AddSingleton<ISystemControlService, SystemControlMockService>();
            else
                services.AddSingleton<ISystemControlService, SystemControlService>();

            // Diagnostics
            services.AddHostedService<SvxLinkDiagnosticsHostedService>();

            // UI Services
            services.AddSingleton<ToastService>();
            services.AddScoped<SetupWizardState>();

            services.AddRazorPages();
            services.AddServerSideBlazor();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
            {
                app.UseExceptionHandler("/Error");
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
