using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Service d'initialisation automatique du SA818Aggregate au démarrage de l'application.
/// Crée l'aggregate avec des valeurs par défaut si absent (idempotent).
/// </summary>
public class SA818InitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SA818InitializerHostedService> _logger;
    private readonly IHostEnvironment _environment;

    public SA818InitializerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SA818InitializerHostedService> logger,
        IHostEnvironment environment)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Exécuté au démarrage de l'application.
    /// Vérifie l'existence du SA818 et le crée avec valeurs par défaut si absent.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SA818InitializerHostedService: Vérification existence SA818Aggregate...");

        try
        {
            // Créer un scope pour résoudre les services scoped
            using var scope = _scopeFactory.CreateScope();
            var sa818Repository = scope.ServiceProvider.GetRequiredService<ISA818Repository>();
            
            // Vérifier si le SA818 existe déjà via la projection (retourne null si absent)
            var existingConfig = await sa818Repository.GetConfigurationAsync(cancellationToken);

            if (existingConfig is not null)
            {
                _logger.LogInformation(
                    "SA818 déjà existant (ID: {SA818Id}), initialisation ignorée.",
                    SA818Aggregate.FixedId);
                return;
            }

            // SA818 absent : créer avec valeurs par défaut
            if (_environment.IsProduction())
            {
                _logger.LogWarning(
                    "SA818InitializerHostedService: ATTENTION — SA818 absent en environnement Production. " +
                    "Création avec valeurs par défaut. Si une configuration était attendue, vérifiez le chemin de la base SQLite.");
            }
            else
            {
                _logger.LogInformation("SA818 non trouvé, création avec valeurs par défaut...");
            }

            var createResult = SA818Aggregate.Create(
                volume: 4,
                squelch: 4,
                bandwidth: SA818Bandwidth.Narrow12_5kHz,
                preEmph: false,
                highPass: false,
                lowPass: false);

            // Gérer le résultat de la validation
            await createResult.Match(
                async aggregate =>
                {
                    var saveResult = await sa818Repository.SaveAsync(aggregate, cancellationToken);
                    
                    saveResult.Match(
                        _ =>
                        {
                            _logger.LogInformation(
                                "SA818 initialisé avec succès (ID: {SA818Id}, Volume: 4, Squelch: 4, Bandwidth: Narrow12_5kHz)",
                                SA818Aggregate.FixedId);
                        },
                        errors =>
                        {
                            _logger.LogError(
                                "Erreur lors de la sauvegarde du SA818: {Errors}",
                                string.Join(", ", errors.Select(e => e.Message)));
                        });
                },
                errors =>
                {
                    _logger.LogError(
                        "Erreur lors de la création du SA818: {Errors}",
                        string.Join(", ", errors.Select(e => e.Message)));
                    return Task.CompletedTask;
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur critique lors de l'initialisation du SA818");
            // Ne pas throw : on ne veut pas empêcher le démarrage de l'application
        }
    }

    /// <summary>
    /// Exécuté à l'arrêt de l'application (rien à faire).
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
