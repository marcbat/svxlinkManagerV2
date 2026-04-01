using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service hébergé déployant le Logic.tcl SVXLink au démarrage de l'application.
/// Le Logic.tcl est déployé une seule fois au démarrage (pas à chaque activation de salon).
/// Il surcharge proc startup {} pour jouer l'annonce du salon (one-shot) au redémarrage du daemon.
/// </summary>
public class LogicTclInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogicTclInitializerHostedService> _logger;

    public LogicTclInitializerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<LogicTclInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Exécuté au démarrage de l'application : déploie Logic.tcl vers SVXLink events.d/local/.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "LogicTclInitializerHostedService: Déploiement du Logic.tcl SVXLink...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ILogicTclDeploymentService>();

            var result = await service.DeployAsync(cancellationToken);

            result.Match(
                Succ: _ =>
                {
                    _logger.LogInformation(
                        "Logic.tcl déployé avec succès au démarrage de l'application.");
                    return LanguageExt.Prelude.unit;
                },
                Fail: errors =>
                {
                    _logger.LogWarning(
                        "Échec du déploiement du Logic.tcl: {Errors}. " +
                        "L'annonce one-shot ne sera pas jouée au prochain switch de salon.",
                        string.Join(", ", errors.Select(e => e.Message)));
                    return LanguageExt.Prelude.unit;
                });
        }
        catch (Exception ex)
        {
            // Ne pas bloquer le démarrage de l'application
            _logger.LogWarning(
                ex,
                "Erreur lors du déploiement du Logic.tcl. L'application continue sans annonce one-shot.");
        }
    }

    /// <summary>
    /// Exécuté à l'arrêt de l'application (rien à faire).
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
