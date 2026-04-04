using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Infrastructure.Hardware;
using SvxlinkManagerV2.Infrastructure.Network;
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

            // Enregistrement des repositories
            services.AddScoped<ISA818Repository, SA818Repository>();
            services.AddScoped<ISalonRepository, SalonRepository>();
            services.AddScoped<ISoundRepository, SoundRepository>();
            services.AddScoped<IGeneralConfigurationRepository, GeneralConfigurationRepository>();
            services.AddScoped<IReflectorRepository, ReflectorRepository>();

            // SA818 initializer
            services.AddHostedService<SA818InitializerHostedService>();

            // Seeding des salons originaux
            services.AddHostedService<SalonSeederHostedService>();

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

            // SVXLink services
            services.AddSingleton<ISvxLinkLogService, SvxLinkLogBuffer>();
            services.AddSingleton<IConnectedNodesService, ConnectedNodesTracker>();
            services.AddSingleton<IDtmfCommandTracker, DtmfCommandTracker>();
            services.AddSingleton<ISvxLinkDaemonService, SvxLinkDaemonService>();
            services.AddScoped<ISvxLinkConfigurationService, SvxLinkConfigurationService>();
            services.AddScoped<ISoundFileDeploymentService, SoundFileDeploymentService>();
            services.AddScoped<ILogicTclDeploymentService, LogicTclDeploymentService>();
            services.AddHostedService<LogicTclInitializerHostedService>();
            services.AddHostedService<DtmfSalonSwitchService>();

            // Reflector services
            services.AddSingleton<IReflectorLogService, ReflectorLogBuffer>();
            services.AddSingleton<IReflectorDaemonService, ReflectorDaemonService>();
            services.AddScoped<IReflectorConfigurationService, ReflectorConfigurationService>();

            // Diagnostics
            services.AddHostedService<SvxLinkDiagnosticsHostedService>();

            // UI Services
            services.AddSingleton<ToastService>();
            services.AddScoped<AudioService>();

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
