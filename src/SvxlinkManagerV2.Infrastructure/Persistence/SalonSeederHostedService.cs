using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Service de seeding automatique des 7 salons originaux au premier démarrage de l'application.
/// Idempotent : si des salons existent déjà, le seeding est ignoré.
/// Si le wizard de configuration initiale est requis, le seeding est également ignoré.
/// Les 6 premiers salons utilisent le protocole V2 (réflecteurs distants),
/// le 7ème ("Réflecteur Local") utilise le protocole V3 (réflecteur local, certificats X.509).
/// </summary>
public class SalonSeederHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SalonSeederHostedService> _logger;
    private readonly IHostEnvironment _environment;
    private readonly ISetupStatusService _setupStatusService;

    public SalonSeederHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SalonSeederHostedService> logger,
        IHostEnvironment environment,
        ISetupStatusService setupStatusService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _environment = environment;
        _setupStatusService = setupStatusService;
    }

    /// <summary>
    /// Exécuté au démarrage de l'application.
    /// Sème les 7 salons originaux si la base est vide (idempotent).
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SalonSeederHostedService: Vérification existence des salons...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var salonRepository = scope.ServiceProvider.GetRequiredService<ISalonRepository>();

            var existingSalons = await salonRepository.GetAllAsync(cancellationToken);

            if (existingSalons.Count > 0)
            {
                _logger.LogInformation(
                    "Salons déjà existants ({Count}), initialisation ignorée.",
                    existingSalons.Count);
                return;
            }

            // Si la base est vide, le wizard de configuration initiale prend en charge le seeding
            var setupRequired = await _setupStatusService.IsSetupRequiredAsync(cancellationToken);
            if (setupRequired)
            {
                _logger.LogInformation(
                    "SalonSeederHostedService: wizard de configuration requis — seeding automatique ignoré.");
                return;
            }

            if (_environment.IsProduction())
            {
                _logger.LogWarning(
                    "SalonSeederHostedService: ATTENTION — aucun salon trouvé en environnement Production. " +
                    "Démarrage du seeding des salons par défaut. Si des données étaient attendues, vérifiez le chemin de la base SQLite.");
            }
            else
            {
                _logger.LogInformation("Aucun salon trouvé, seeding des 7 salons originaux...");
            }

            foreach (var (id, name, host, port, authKey, dtmfCode, protocol) in GetOriginalSalons())
            {
                var configuration = new SvxLinkConfiguration(
                    Id: Guid.NewGuid(),
                    Logics: "SimplexLogic,ReflectorLogic",
                    CfgDir: "svxlink.d",
                    CardSampleRate: 16000,
                    CardChannels: 1,
                    Host: host,
                    Port: port,
                    Callsign: "NOCALL",
                    AuthKey: authKey,
                    JitterBufferDelay: 0,
                    ReflectorProtocol: protocol,
                    CertEmail: null,
                    SimplexCallsign: "F0ABC",
                    Modules: "ModuleHelp",
                    ShortIdentInterval: 600,
                    LongIdentInterval: 3600,
                    ReportCtcss: null,
                    DefaultLang: "fr_FR",
                    RgrSoundDelay: 0,
                    RxFrequency: 145.550m,
                    TxFrequency: 145.550m,
                    RxCtcss: null,
                    TxCtcss: null);

                var createResult = SalonAggregate.Create(
                    id: id,
                    name: name,
                    isDefault: false,
                    configuration: configuration);

                await createResult.Match(
                    async aggregate =>
                    {
                        if (dtmfCode.HasValue)
                            aggregate.UpdateDtmfCode(dtmfCode.Value);

                        var saveResult = await salonRepository.SaveAsync(aggregate, cancellationToken);

                        saveResult.Match(
                            _ =>
                            {
                                _logger.LogInformation(
                                    "Salon seedé avec succès : {Name} (ID: {Id})",
                                    name,
                                    id);
                            },
                            errors =>
                            {
                                _logger.LogError(
                                    "Erreur lors de la sauvegarde du salon '{Name}': {Errors}",
                                    name,
                                    string.Join(", ", errors.Select(e => e.Message)));
                            });
                    },
                    errors =>
                    {
                        _logger.LogError(
                            "Erreur lors de la création du salon '{Name}': {Errors}",
                            name,
                            string.Join(", ", errors.Select(e => e.Message)));
                        return Task.CompletedTask;
                    });
            }

            _logger.LogInformation("Seeding des salons originaux terminé.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur critique lors du seeding des salons — seeding interrompu");
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

    /// <summary>
    /// Retourne les 7 salons originaux avec leurs GUIDs fixes (compatibilité migration legacy).
    /// Les 6 premiers sont des réflecteurs distants (protocole V2), le 7ème est le réflecteur local (protocole V3).
    /// </summary>
    private static IEnumerable<(Guid Id, string Name, string Host, int Port, string? AuthKey, int? DtmfCode, ReflectorProtocol Protocol)> GetOriginalSalons()
    {
        yield return (new Guid("235a4521-15a1-4e02-a540-91ee600452ac"), "Réseau des Répéteurs Francophones", "rrf2.f5nlg.ovh", 5300, "Magnifique123456789!", 96, ReflectorProtocol.V2);
        yield return (new Guid("1f2e87b8-d984-4c05-8a4a-ffad65c829a9"), "Salon Suisse Romand", "salonsuisseromand.hbspot.ch", 5300, "xD9wW5gO7yD9hN5o", 200, ReflectorProtocol.V2);
        yield return (new Guid("0f669a03-dcf1-4277-9b07-54f6a0fd3037"), "French Open Network", "serveur.f1tzo.com", 5300, "FON-F1TZO", 97, ReflectorProtocol.V2);
        yield return (new Guid("a749ffe5-16c7-45da-809d-c048908f115c"), "Salon Technique", "rrf3.f5nlg.ovh", 5301, "Magnifique123456789!", 98, ReflectorProtocol.V2);
        yield return (new Guid("d4c59d86-947c-4b1d-831a-807c1877d426"), "Salon Bavardage", "serveur.f1tzo.com", 5301, "FON-F1TZO", 100, ReflectorProtocol.V2);
        yield return (new Guid("9f99b18b-96ea-453d-b07a-7923c09c939f"), "Salon Local", "serveur.f1tzo.com", 5302, "FON-F1TZO", 101, ReflectorProtocol.V2);
        yield return (new Guid("c7a3e2d1-4b8f-4e6a-9d2c-1f5b7e8a3c04"), "Réflecteur Local", "127.0.0.1", 5300, null, 210, ReflectorProtocol.V3);
    }
}
