using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.Reflectors.ActivateReflector;
using SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;
using SvxlinkManagerV2.Application.Interfaces;
using Wolverine;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Service d'activation automatique au démarrage selon la configuration générale.
/// S'exécute après SA818InitializerHostedService (enregistré après dans le DI).
/// Active le réflecteur et/ou le salon par défaut selon les options configurées.
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

            if (generalConfig is null)
            {
                _logger.LogInformation(
                    "StartupActivationHostedService: Aucune configuration générale trouvée, activation automatique ignorée.");
                return;
            }

            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            // Activation du réflecteur au démarrage
            if (generalConfig.StartReflectorOnStartup)
            {
                await TryActivateReflectorAsync(scope, bus, cancellationToken);
            }

            // Activation du salon par défaut au démarrage
            if (generalConfig.StartDefaultSalonOnStartup)
            {
                await TryActivateDefaultSalonAsync(scope, bus, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartupActivationHostedService: Erreur lors de l'activation automatique au démarrage");
            // Ne pas throw : on ne veut pas empêcher le démarrage de l'application
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task TryActivateReflectorAsync(IServiceScope scope, IMessageBus bus, CancellationToken cancellationToken)
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

            var result = await bus.InvokeAsync<LanguageExt.Validation<SvxlinkManagerV2.Domain.Common.Error, LanguageExt.Unit>>(
                new ActivateReflectorCommand(activeReflector.Id), cancellationToken);

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

    private async Task TryActivateDefaultSalonAsync(IServiceScope scope, IMessageBus bus, CancellationToken cancellationToken)
    {
        try
        {
            var salonRepository = scope.ServiceProvider.GetRequiredService<ISalonRepository>();
            var defaultSalon = await salonRepository.GetDefaultAsync(cancellationToken);

            if (defaultSalon is null || defaultSalon.IsDeleted)
            {
                _logger.LogWarning("StartupActivationHostedService: Aucun salon par défaut disponible pour l'activation automatique.");
                return;
            }

            _logger.LogInformation(
                "StartupActivationHostedService: Activation automatique du salon par défaut {Id} ({Name})...",
                defaultSalon.Id, defaultSalon.Name);

            var result = await bus.InvokeAsync<LanguageExt.Validation<SvxlinkManagerV2.Domain.Common.Error, LanguageExt.Unit>>(
                new ActivateSalonCommand(defaultSalon.Id), cancellationToken);

            result.Match(
                _ => _logger.LogInformation(
                    "StartupActivationHostedService: Salon {Id} activé avec succès.", defaultSalon.Id),
                errors => _logger.LogError(
                    "StartupActivationHostedService: Échec de l'activation du salon : {Errors}",
                    string.Join(", ", errors.Select(e => e.Message))));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartupActivationHostedService: Erreur lors de l'activation du salon par défaut");
        }
    }
}
