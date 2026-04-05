using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Sound;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Service de seeding automatique des 6 salons originaux au premier démarrage de l'application.
/// Idempotent : si des salons existent déjà, le seeding est ignoré.
/// Les fichiers audio présents dans le répertoire configuré (Seed:AudioDirectory) sont
/// automatiquement associés à leur salon correspondant au moment du seeding.
/// </summary>
public class SalonSeederHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SalonSeederHostedService> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public SalonSeederHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SalonSeederHostedService> logger,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _environment = environment;
        _configuration = configuration;
    }

    /// <summary>
    /// Exécuté au démarrage de l'application.
    /// Sème les 8 salons originaux si la base est vide (idempotent).
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SalonSeederHostedService: Vérification existence des salons...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var salonRepository = scope.ServiceProvider.GetRequiredService<ISalonRepository>();
            var soundRepository = scope.ServiceProvider.GetRequiredService<ISoundRepository>();

            var existingSalons = await salonRepository.GetAllAsync(cancellationToken);

            if (existingSalons.Count > 0)
            {
                _logger.LogInformation(
                    "Salons déjà existants ({Count}), initialisation ignorée.",
                    existingSalons.Count);
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
                _logger.LogInformation("Aucun salon trouvé, seeding des 6 salons originaux...");
            }

            var audioDirectory = _configuration["Seed:AudioDirectory"] ?? "/app/audio";

            foreach (var (id, name, host, port, authKey, dtmfCode, soundId, audioFileName) in GetOriginalSalons())
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
                    SimplexCallsign: "F0ABC",
                    Modules: "ModuleHelp",
                    ShortIdentInterval: 600,
                    LongIdentInterval: 3600,
                    ReportCtcss: null,
                    EventHandler: "/usr/share/svxlink/events.tcl",
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
                    isTemporized: false,
                    configuration: configuration);

                await createResult.Match(
                    async aggregate =>
                    {
                        if (dtmfCode.HasValue)
                            aggregate.UpdateDtmfCode(dtmfCode.Value);

                        await TrySeedSoundAsync(soundRepository, aggregate, soundId, audioFileName, audioDirectory, cancellationToken);

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

    private async Task TrySeedSoundAsync(
        ISoundRepository soundRepository,
        SalonAggregate aggregate,
        Guid soundId,
        string audioFileName,
        string audioDirectory,
        CancellationToken cancellationToken)
    {
        var audioFilePath = Path.Combine(audioDirectory, audioFileName);

        if (!File.Exists(audioFilePath))
        {
            _logger.LogWarning(
                "Fichier audio introuvable, son non assigné au salon : {Path}",
                audioFilePath);
            return;
        }

        var fileContent = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
        var soundName = Path.GetFileNameWithoutExtension(audioFileName);
        var soundResult = SoundAggregate.Create(soundId, soundName, fileContent);

        await soundResult.Match(
            async sound =>
            {
                var saveResult = await soundRepository.SaveAsync(sound, cancellationToken);

                saveResult.Match(
                    _ =>
                    {
                        aggregate.AssignSound(soundId);
                        _logger.LogInformation(
                            "Son seedé et assigné au salon : {FileName} → {SalonName}",
                            audioFileName,
                            aggregate.Name);
                    },
                    errors =>
                    {
                        _logger.LogError(
                            "Erreur lors de la sauvegarde du son '{Name}': {Errors}",
                            soundName,
                            string.Join(", ", errors.Select(e => e.Message)));
                    });
            },
            errors =>
            {
                _logger.LogError(
                    "Erreur lors de la création du son '{Name}': {Errors}",
                    audioFileName,
                    string.Join(", ", errors.Select(e => e.Message)));
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Retourne les 6 salons originaux avec leurs GUIDs fixes (compatibilité migration legacy).
    /// Chaque entrée inclut le code DTMF, le GUID fixe du son associé et le nom du fichier audio.
    /// </summary>
    private static IEnumerable<(Guid Id, string Name, string Host, int Port, string AuthKey, int? DtmfCode, Guid SoundId, string AudioFileName)> GetOriginalSalons()
    {
        yield return (new Guid("235a4521-15a1-4e02-a540-91ee600452ac"), "Réseau des Répéteurs Francophones", "rrf2.f5nlg.ovh", 5300, "Magnifique123456789!", 96, new Guid("235a4521-0000-0000-0000-000000000001"), "reseauRepeteurFrancophone.wav");
        yield return (new Guid("1f2e87b8-d984-4c05-8a4a-ffad65c829a9"), "Salon Suisse Romand", "salonsuisseromand.hbspot.ch", 5300, "xD9wW5gO7yD9hN5o", 200, new Guid("1f2e87b8-0000-0000-0000-000000000001"), "SalonSuisseRomand.wav");
        yield return (new Guid("0f669a03-dcf1-4277-9b07-54f6a0fd3037"), "French Open Network", "serveur.f1tzo.com", 5300, "FON-F1TZO", 97, new Guid("0f669a03-0000-0000-0000-000000000001"), "frenchOpenNetwork.wav");
        yield return (new Guid("a749ffe5-16c7-45da-809d-c048908f115c"), "Salon Technique", "rrf3.f5nlg.ovh", 5301, "Magnifique123456789!", 98, new Guid("a749ffe5-0000-0000-0000-000000000001"), "salonTechnique.wav");
        yield return (new Guid("d4c59d86-947c-4b1d-831a-807c1877d426"), "Salon Bavardage", "serveur.f1tzo.com", 5301, "FON-F1TZO", 100, new Guid("d4c59d86-0000-0000-0000-000000000001"), "salonBavardage.wav");
        yield return (new Guid("9f99b18b-96ea-453d-b07a-7923c09c939f"), "Salon Local", "serveur.f1tzo.com", 5302, "FON-F1TZO", 101, new Guid("9f99b18b-0000-0000-0000-000000000001"), "salonLocal.wav");
    }
}
