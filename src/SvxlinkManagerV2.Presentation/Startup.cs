using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Application.Features.Reflectors;
using SvxlinkManagerV2.Application.Features.Statistics;
using SvxlinkManagerV2.Application.Features.SystemStatus;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Infrastructure.Hardware;
using SvxlinkManagerV2.Infrastructure.Monitoring;
using SvxlinkManagerV2.Infrastructure.Network;
using SvxlinkManagerV2.Infrastructure.Network.Apt;
using SvxlinkManagerV2.Infrastructure.Persistence;
using SvxlinkManagerV2.Infrastructure.Persistence.Repositories;
using SvxlinkManagerV2.Infrastructure.Reflector;
using SvxlinkManagerV2.Infrastructure.Runtime;
using SvxlinkManagerV2.Infrastructure.Statistics;
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

            // ASP.NET Identity - compte unique administrateur
            services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.SignIn.RequireConfirmedAccount = false;

                // Verrouillage temporaire après échecs répétés (protection anti-force brute)
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<SvxlinkDbContext>()
            .AddDefaultTokenProviders();

            // Options du cookie d'authentification
            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/account/logout";
                options.AccessDeniedPath = "/login";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

            // Fallback policy : toutes les routes requièrent une authentification par défaut
            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });

            // État d'authentification pour Blazor Server
            services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();

            // Gestion du compte utilisateur
            services.AddScoped<IUserAccountService, UserAccountService>();
            services.AddSingleton<IPendingSetupLoginService, PendingSetupLoginService>();

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
            services.AddScoped<IAudioConfigurationRepository, AudioConfigurationRepository>();
            services.AddScoped<IActivityRepository, ActivityRepository>();

            // Historique d'activité (page Statistiques).
            // Le recorder est exposé sous les deux formes : l'interface pour les handlers MediatR,
            // le type concret pour le service hébergé qui doit en piloter la même instance.
            services.Configure<StatisticsOptions>(Configuration.GetSection(StatisticsOptions.SectionName));
            services.AddSingleton<ActivityRecorder>();
            services.AddSingleton<IActivityRecorder>(sp => sp.GetRequiredService<ActivityRecorder>());

            // ATTENTION : doit rester enregistré AVANT StartupActivationHostedService.
            // Son démarrage clôt les sessions laissées ouvertes par un arrêt brutal ; placé après
            // l'activation automatique, il refermerait la session que celle-ci vient d'ouvrir.
            services.AddHostedService<ActivityRecorderHostedService>();
            services.AddHostedService<StatisticsPurgeHostedService>();

            // SA818 initializer
            services.AddHostedService<SA818InitializerHostedService>();

            // Adresse du réflecteur local, vers laquelle pointe le salon « Réflecteur Local »
            // seedé. Lue avant les seeders, qui en dépendent tous les deux.
            services.Configure<LocalReflectorOptions>(Configuration.GetSection(LocalReflectorOptions.SectionName));

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

            // Chaîne audio de la machine : niveaux ALSA et test d'émission (réels ou simulés)
            services.Configure<AudioOptions>(Configuration.GetSection(AudioOptions.SectionName));
            var useAudioMock = Configuration.GetValue<bool>($"{AudioOptions.SectionName}:UseMock", false);
            if (useAudioMock)
            {
                services.AddSingleton<IAudioService, AudioMockService>();
                services.AddSingleton<IPttTestService, PttTestMockService>();
            }
            else
            {
                services.AddSingleton<IAudioService, AlsaAudioService>();
                services.AddSingleton<IPttTestService, GpioPttTestService>();
            }

            // Réapplication des niveaux mémorisés au démarrage
            services.AddHostedService<AudioInitializerHostedService>();

            // WiFi service (réel ou mock)
            var useWifiMock = Configuration.GetValue<bool>("Wifi:UseMock", false);
            if (useWifiMock)
                services.AddScoped<IWifiService, WifiMockService>();
            else
                services.AddScoped<IWifiService, WifiService>();

            // Mise à jour applicative via le dépôt APT du projet. Le dépôt étant public et
            // signé, il n'y a plus ni token ni téléchargement maison : apt résout les
            // dépendances, compare les versions et installe.
            services.Configure<AptUpdateOptions>(Configuration.GetSection(AptUpdateOptions.SectionName));
            services.AddSingleton<IAptCommandRunner, AptCommandRunner>();
            services.AddSingleton<IAptSourceManager, AptSourceManager>();
            services.AddSingleton<IApplicationUpdateService, AptApplicationUpdateService>();
            services.AddSingleton<IApplicationUpdateWorkflowService, AptApplicationUpdateWorkflowService>();

            // SVXLink version strategies (dual install: 19.09.2 legacy + 25.05 modern)
            services.AddSingleton<ISvxLinkVersionStrategy, SvxLinkLegacyStrategy>();
            services.AddSingleton<ISvxLinkVersionStrategy, SvxLinkModernStrategy>();
            services.AddSingleton<ISvxLinkStrategyResolver, SvxLinkStrategyResolver>();

            // SVXLink services
            services.AddSingleton<ISvxLinkLogService, SvxLinkLogBuffer>();
            services.AddSingleton<IConnectedNodesService, ConnectedNodesTracker>();
            services.AddSingleton<IReflectorLinkStateService, ReflectorLinkStateTracker>();
            services.AddSingleton<IDtmfCommandTracker, DtmfCommandTracker>();
            services.AddSingleton<IRxDistortionService, RxDistortionTracker>();
            services.AddSingleton<ISquelchStateService, SquelchStateTracker>();
            services.AddSingleton<ISvxLinkDaemonService, SvxLinkDaemonService>();
            services.AddScoped<ISvxLinkConfigurationService, SvxLinkConfigurationService>();
            services.AddSingleton<ISvxLinkConfigurationReader, SvxLinkConfigurationReader>();
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

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                // Le hub Blazor doit rester accessible anonymement, sinon la négociation
                // du circuit boucle en ERR_TOO_MANY_REDIRECTS.
                endpoints.MapBlazorHub().AllowAnonymous();

                // La page hôte porte son propre [AllowAnonymous] (voir _Host.cshtml) :
                // toutes les routes Blazor étant servies par ce même fallback, le
                // middleware ne peut pas les distinguer. L'autorisation des routes
                // Blazor est donc portée par AuthorizeRouteView (App.razor) + le
                // [Authorize] par défaut de _Imports.razor.
                // Attention : .AllowAnonymous() posé ici en convention n'atteint pas
                // l'endpoint de fallback Razor Pages — d'où l'attribut sur la page.
                endpoints.MapFallbackToPage("/_Host");

                // La FallbackPolicy reste en place : elle protège les Razor Pages
                // explicites qui seraient ajoutées sans attribut d'autorisation.
                endpoints.MapRazorPages();
            });
        }
    }
}
