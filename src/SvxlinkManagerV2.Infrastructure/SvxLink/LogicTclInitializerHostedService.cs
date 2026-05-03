using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service hébergé déployant le Logic.tcl SVXLink au démarrage de l'application.
/// Le Logic.tcl est déployé une seule fois au démarrage (pas à chaque activation de salon).
/// Il déploie le fichier qui gère les commandes DTMF (dont 398 et 399 pour les annonces sonores).
/// L'annonce de connexion est désormais déclenchée par ReflectorConnectionAnnouncementService
/// depuis .NET, une fois la connexion réelle au réflecteur confirmée dans les logs.
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
                        "Les commandes DTMF d'annonce ne seront pas disponibles.",
                        string.Join(", ", errors.Select(e => e.Message)));
                    return LanguageExt.Prelude.unit;
                });
        }
        catch (Exception ex)
        {
            // Ne pas bloquer le démarrage de l'application
            _logger.LogWarning(
                ex,
                "Erreur lors du déploiement du Logic.tcl. L'application continue sans les commandes DTMF d'annonce.");
        }
    }

    /// <summary>
    /// Exécuté à l'arrêt de l'application (rien à faire).
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
