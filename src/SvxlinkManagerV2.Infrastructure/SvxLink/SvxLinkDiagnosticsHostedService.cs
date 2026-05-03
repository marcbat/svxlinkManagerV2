using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service de démarrage qui affiche un banner de diagnostics SVXLink dans la console.
/// Indique : mode daemon, état du fichier de configuration, paramètres du reflector,
/// et si SVXLink est actuellement actif.
/// </summary>
public class SvxLinkDiagnosticsHostedService : IHostedService
{
    private readonly ISvxLinkDaemonService _daemonService;
    private readonly ILogger<SvxLinkDiagnosticsHostedService> _logger;
    private readonly IConfiguration _configuration;

    public SvxLinkDiagnosticsHostedService(
        ISvxLinkDaemonService daemonService,
        ILogger<SvxLinkDiagnosticsHostedService> logger,
        IConfiguration configuration)
    {
        _daemonService = daemonService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configPath = _configuration["SvxLink:ConfigPath"] ?? "/etc/svxlink/svxlink.conf";
        var useMockDaemon = _configuration.GetValue<bool>("SvxLink:UseMockDaemon", false);

        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger.LogInformation("  SvxLink Manager V2 — Diagnostics démarrage");
        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _logger.LogInformation("  Mode daemon    : {Mode}", useMockDaemon
            ? "MOCK (simulation, pas de processus svxlink)"
            : "RÉEL (processus svxlink géré par l'application)");
        _logger.LogInformation("  Config SVXLink : {Path}", configPath);

        // Vérification et lecture du fichier de configuration
        if (File.Exists(configPath))
        {
            _logger.LogInformation("  Fichier config : PRÉSENT");
            await LogReflectorParametersAsync(configPath, cancellationToken);
        }
        else
        {
            _logger.LogWarning("  Fichier config : ABSENT — sera généré lors de l'activation d'un salon");
        }

        // Vérification de l'état du daemon SVXLink
        try
        {
            var isRunning = await _daemonService.IsRunningAsync(cancellationToken);
            var running = isRunning.Match(v => v, _ => false);

            if (running)
                _logger.LogInformation("  SVXLink actif  : OUI");
            else
                _logger.LogInformation("  SVXLink actif  : NON — démarrera lors de l'activation d'un salon");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "  SVXLink actif  : impossible de vérifier l'état");
        }

        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Lit le fichier svxlink.conf et affiche les paramètres [ReflectorLogic] dans les logs.
    /// </summary>
    private async Task LogReflectorParametersAsync(string configPath, CancellationToken cancellationToken)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(configPath, cancellationToken);
            var inReflector = false;
            var host = "(non défini)";
            var callsign = "(non défini)";
            var port = "(non défini)";

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed == "[ReflectorLogic]") { inReflector = true; continue; }
                if (inReflector && trimmed.StartsWith("[")) break;
                if (!inReflector) continue;

                if (trimmed.StartsWith("HOST="))          host     = trimmed[5..];
                else if (trimmed.StartsWith("PORT="))     port     = trimmed[5..];
                else if (trimmed.StartsWith("CALLSIGN=")) callsign = trimmed[9..];
            }

            _logger.LogInformation("  Reflector HOST : {Host}:{Port}", host, port);
            _logger.LogInformation("  Callsign nœud  : {Callsign}", callsign);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "  Impossible de lire les paramètres [ReflectorLogic]");
        }
    }
}
