using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.Reflectors.ActivateReflector;
using SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;
using SvxlinkManagerV2.Application.Features.Salons.ActivateStandaloneMode;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Service d'activation automatique au démarrage selon la configuration générale.
/// S'exécute après SA818InitializerHostedService (enregistré après dans le DI).
/// Au démarrage, SVXLink est toujours lancé : soit avec le salon par défaut,
/// soit en mode standalone (simplex sans réflecteur) pour l'écoute DTMF.
/// </summary>
public class StartupActivationHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StartupActivationHostedService> _logger;

    public StartupActivationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<StartupActivationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StartupActivationHostedService: Vérification de la configuration de démarrage automatique...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var generalConfigRepo = scope.ServiceProvider.GetRequiredService<IGeneralConfigurationRepository>();
            var generalConfig = await generalConfigRepo.GetAsync(cancellationToken);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            if (generalConfig is not null && generalConfig.StartReflectorOnStartup)
                await TryActivateReflectorAsync(scope, mediator, cancellationToken);

            if (generalConfig is not null && generalConfig.StartDefaultSalonOnStartup)
            {
                var salonActivated = await TryActivateDefaultSalonAsync(scope, mediator, cancellationToken);
                if (salonActivated)
                    return;
            }

            // Si aucun salon n'a été activé, démarrer en mode standalone pour l'écoute DTMF
            _logger.LogInformation(
                "StartupActivationHostedService: Démarrage de SVXLink en mode standalone (écoute DTMF sans réflecteur)...");
            await TryActivateStandaloneModeAsync(mediator, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartupActivationHostedService: Erreur lors de l'activation automatique au démarrage");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task TryActivateReflectorAsync(IServiceScope scope, IMediator mediator, CancellationToken cancellationToken)
    {
        try
        {
            var reflectorRepository = scope.ServiceProvider.GetRequiredService<IReflectorRepository>();
            var reflectors = await reflectorRepository.GetAllAsync(cancellationToken);

            var activeReflector = reflectors.FirstOrDefault(r => !r.IsDeleted);
            if (activeReflector is null)
            {
                _logger.LogWarning("StartupActivationHostedService: Aucun réflecteur disponible pour l'activation automatique.");
                return;
            }

            _logger.LogInformation(
                "StartupActivationHostedService: Activation automatique du réflecteur {Id}...",
                activeReflector.Id);

            var result = await mediator.Send(new ActivateReflectorCommand(activeReflector.Id), cancellationToken);

            result.Match(
                _ => _logger.LogInformation(
                    "StartupActivationHostedService: Réflecteur {Id} activé avec succès.", activeReflector.Id),
                errors => _logger.LogError(
                    "StartupActivationHostedService: Échec de l'activation du réflecteur : {Errors}",
                    string.Join(", ", errors.Select(e => e.Message))));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartupActivationHostedService: Erreur lors de l'activation du réflecteur");
        }
    }

    private async Task<bool> TryActivateDefaultSalonAsync(IServiceScope scope, IMediator mediator, CancellationToken cancellationToken)
    {
        try
        {
            var salonRepository = scope.ServiceProvider.GetRequiredService<ISalonRepository>();
            var defaultSalon = await salonRepository.GetDefaultAsync(cancellationToken);

            if (defaultSalon is null || defaultSalon.IsDeleted)
            {
                _logger.LogWarning("StartupActivationHostedService: Aucun salon par défaut disponible pour l'activation automatique.");
                return false;
            }

            _logger.LogInformation(
                "StartupActivationHostedService: Activation automatique du salon par défaut {Id} ({Name})...",
                defaultSalon.Id, defaultSalon.Name);

            var result = await mediator.Send(new ActivateSalonCommand(defaultSalon.Id, SalonActivationOrigin.Startup), cancellationToken);

            return result.Match(
                _ =>
                {
                    _logger.LogInformation(
                        "StartupActivationHostedService: Salon {Id} activé avec succès.", defaultSalon.Id);
                    return true;
                },
                errors =>
                {
                    _logger.LogError(
                        "StartupActivationHostedService: Échec de l'activation du salon : {Errors}",
                        string.Join(", ", errors.Select(e => e.Message)));
                    return false;
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartupActivationHostedService: Erreur lors de l'activation du salon par défaut");
            return false;
        }
    }

    private async Task TryActivateStandaloneModeAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new ActivateStandaloneModeCommand(SalonActivationOrigin.Startup), cancellationToken);

            result.Match(
                _ => _logger.LogInformation(
                    "StartupActivationHostedService: Mode standalone activé avec succès."),
                errors => _logger.LogError(
                    "StartupActivationHostedService: Échec de l'activation du mode standalone : {Errors}",
                    string.Join(", ", errors.Select(e => e.Message))));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartupActivationHostedService: Erreur lors de l'activation du mode standalone");
        }
    }
}
