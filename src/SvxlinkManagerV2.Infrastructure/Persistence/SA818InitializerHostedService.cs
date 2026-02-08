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
    private readonly ISA818Repository _sa818Repository;
    private readonly ILogger<SA818InitializerHostedService> _logger;

    public SA818InitializerHostedService(
        ISA818Repository sa818Repository,
        ILogger<SA818InitializerHostedService> logger)
    {
        _sa818Repository = sa818Repository;
        _logger = logger;
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
            // Vérifier si le SA818 existe déjà via la projection (retourne null si absent)
            var existingConfig = await _sa818Repository.GetConfigurationAsync(cancellationToken);

            if (existingConfig is not null)
            {
                _logger.LogInformation(
                    "SA818 déjà existant (ID: {SA818Id}), initialisation ignorée.",
                    SA818Aggregate.FixedId);
                return;
            }

            // SA818 absent : créer avec valeurs par défaut
            _logger.LogInformation("SA818 non trouvé, création avec valeurs par défaut...");

            var createResult = SA818Aggregate.Create(
                volume: 4,
                squelch: 4,
                bandwidth: SA818Bandwidth.Wide25kHz,
                preEmph: false,
                highPass: false,
                lowPass: false);

            // Gérer le résultat de la validation
            await createResult.Match(
                async aggregate =>
                {
                    var saveResult = await _sa818Repository.SaveAsync(aggregate, cancellationToken);
                    
                    saveResult.Match(
                        _ =>
                        {
                            _logger.LogInformation(
                                "SA818 initialisé avec succès (ID: {SA818Id}, Volume: 4, Squelch: 4, Bandwidth: Wide25kHz)",
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
