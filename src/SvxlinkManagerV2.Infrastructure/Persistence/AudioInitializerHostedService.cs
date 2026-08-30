using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Réapplique les niveaux ALSA mémorisés au démarrage de l'application, afin qu'un réglage fait
/// depuis la page audio survive à un redémarrage — le service <c>alsa-restore</c> du système
/// remettant sinon l'état enregistré dans <c>/var/lib/alsa/asound.state</c> au boot.
///
/// Au tout premier démarrage, aucune valeur n'est mémorisée : l'application **adopte** alors les
/// niveaux trouvés sur la carte au lieu d'en imposer par défaut. Un nœud radio en service a des
/// niveaux réglés à l'oreille, souvent longuement : les écraser au premier lancement serait une
/// régression pour l'utilisateur. Même prudence si la configuration désigne d'autres contrôles
/// que ceux mémorisés — une valeur ne se transpose pas d'un contrôle à l'autre.
/// </summary>
public class AudioInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAudioService _audioService;
    private readonly ILogger<AudioInitializerHostedService> _logger;

    /// <param name="scopeFactory">Fabrique de scope, pour résoudre le repository.</param>
    /// <param name="audioService">Service de pilotage des niveaux ALSA.</param>
    /// <param name="distortionService">
    /// Détecteur de saturation en réception. Il n'est pas utilisé ici : sa seule résolution force
    /// la création du singleton dès le démarrage, donc son abonnement au flux de logs SVXLink dès
    /// la première ligne — sans quoi il ne s'abonnerait qu'à la première ouverture de la page audio.
    /// </param>
    /// <param name="logger">Journal.</param>
    public AudioInitializerHostedService(
        IServiceScopeFactory scopeFactory,
        IAudioService audioService,
        IRxDistortionService distortionService,
        ILogger<AudioInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _audioService = audioService;
        _logger = logger;
        _ = distortionService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAudioConfigurationRepository>();

            var hardwareResult = await _audioService.GetStateAsync(cancellationToken);
            if (hardwareResult.IsFail)
            {
                _logger.LogWarning(
                    "Niveaux ALSA illisibles au démarrage : {Errors}. Réglages audio laissés en l'état.",
                    Describe(hardwareResult));
                return;
            }

            var hardware = hardwareResult.Match(
                Succ: state => state,
                Fail: _ => throw new InvalidOperationException("Lecture des niveaux déjà validée."));

            var storedResult = await repository.GetAsync(cancellationToken);
            var stored = storedResult.SuccessOrNull();

            if (stored is not null && stored.Targets(hardware.Capture.Name, hardware.Playback.Name))
            {
                await ApplyStoredLevelsAsync(stored, cancellationToken);
                return;
            }

            if (stored is not null)
            {
                _logger.LogWarning(
                    "Les niveaux mémorisés portaient sur « {StoredCapture} » / « {StoredPlayback} », " +
                    "la configuration désigne désormais « {Capture} » / « {Playback} » : " +
                    "les niveaux de la carte sont adoptés tels quels.",
                    stored.CaptureControl, stored.PlaybackControl,
                    hardware.Capture.Name, hardware.Playback.Name);
            }

            await AdoptHardwareLevelsAsync(repository, stored, hardware.Capture.Name, hardware.Capture.Value,
                hardware.Playback.Name, hardware.Playback.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            // Un problème de carte son ne doit jamais empêcher l'application de démarrer.
            _logger.LogError(ex, "Erreur lors de l'initialisation des niveaux audio");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Réapplique à la carte les niveaux mémorisés.
    /// </summary>
    private async Task ApplyStoredLevelsAsync(
        AudioConfigurationAggregate stored,
        CancellationToken cancellationToken)
    {
        var capture = await _audioService.SetCaptureLevelAsync(stored.CaptureLevel, cancellationToken);
        var playback = await _audioService.SetPlaybackLevelAsync(stored.PlaybackLevel, cancellationToken);

        if (capture.IsFail || playback.IsFail)
        {
            _logger.LogError(
                "Les niveaux audio mémorisés n'ont pas pu être réappliqués : {Errors}",
                $"{Describe(capture)} {Describe(playback)}".Trim());
            return;
        }

        _logger.LogInformation(
            "Niveaux audio réappliqués : « {Capture} » = {CaptureLevel}, « {Playback} » = {PlaybackLevel}",
            stored.CaptureControl, stored.CaptureLevel, stored.PlaybackControl, stored.PlaybackLevel);
    }

    /// <summary>
    /// Mémorise les niveaux actuellement réglés sur la carte, sans rien y modifier.
    /// </summary>
    private async Task AdoptHardwareLevelsAsync(
        IAudioConfigurationRepository repository,
        AudioConfigurationAggregate? stored,
        string captureControl,
        int captureLevel,
        string playbackControl,
        int playbackLevel,
        CancellationToken cancellationToken)
    {
        AudioConfigurationAggregate? aggregate = stored;

        if (aggregate is null)
        {
            var createResult = AudioConfigurationAggregate.Create(
                captureControl, captureLevel, playbackControl, playbackLevel);

            if (createResult.IsFail)
            {
                _logger.LogError(
                    "Configuration audio non initialisable : {Errors}",
                    Describe(createResult));
                return;
            }

            aggregate = createResult.Match(
                Succ: created => created,
                Fail: _ => throw new InvalidOperationException("Création déjà validée."));
        }
        else
        {
            var updateResult = aggregate.UpdateLevels(
                captureControl, captureLevel, playbackControl, playbackLevel);

            if (updateResult.IsFail)
            {
                _logger.LogError(
                    "Niveaux audio de la carte non mémorisables : {Errors}",
                    Describe(updateResult));
                return;
            }
        }

        var saveResult = await repository.SaveAsync(aggregate, cancellationToken);

        saveResult.Match(
            Succ: _ =>
            {
                _logger.LogInformation(
                    "Niveaux audio de la carte adoptés sans modification : « {Capture} » = {CaptureLevel}, " +
                    "« {Playback} » = {PlaybackLevel}",
                    captureControl, captureLevel, playbackControl, playbackLevel);
                return Unit.Default;
            },
            Fail: errors =>
            {
                _logger.LogError("Configuration audio non enregistrée : {Errors}", Describe(errors));
                return Unit.Default;
            });
    }

    /// <summary>
    /// Concatène les messages d'erreur d'une validation en échec, pour la journalisation.
    /// </summary>
    private static string Describe<T>(Validation<Error, T> validation) =>
        validation.Match(Succ: _ => string.Empty, Fail: errors => Describe(errors));

    private static string Describe(IEnumerable<Error> errors) =>
        string.Join(", ", errors.Select(error => error.Message));
}
