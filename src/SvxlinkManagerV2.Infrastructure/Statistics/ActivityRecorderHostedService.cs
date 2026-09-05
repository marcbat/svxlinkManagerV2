using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Infrastructure.Statistics;

/// <summary>
/// Branche l'historique d'activité sur les trackers d'infrastructure déjà en place.
///
/// Tout ce qu'observe l'application est déjà publié sous forme d'événements C# par des
/// singletons — nœuds connectés, commandes DTMF, état de liaison, écrêtages, squelch local.
/// Ce service ne fait que les convertir en écritures. Aucune nouvelle analyse de logs n'est
/// faite ici : elle appartient aux trackers.
///
/// **Ordre d'enregistrement** : ce service doit être enregistré AVANT
/// <c>StartupActivationHostedService</c>. Son démarrage clôt les sessions restées ouvertes
/// par un arrêt brutal ; s'il passait après l'activation automatique, il refermerait aussitôt
/// la session que celle-ci vient d'ouvrir.
/// </summary>
public class ActivityRecorderHostedService : IHostedService
{
    private readonly ActivityRecorder _recorder;
    private readonly IConnectedNodesService _connectedNodes;
    private readonly IDtmfCommandTracker _dtmfTracker;
    private readonly IReflectorLinkStateService _linkState;
    private readonly IRxDistortionService _distortion;
    private readonly ISquelchStateService _squelch;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityRecorderHostedService> _logger;

    /// <summary>
    /// Début du passage en cours de chaque nœud entendu. Un passage n'est écrit qu'à sa fin,
    /// avec sa durée : ceux qui restent ouverts (redémarrage, salon quitté) sont abandonnés
    /// plutôt qu'écrits avec une durée inventée.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _talkerStarts = new();

    public ActivityRecorderHostedService(
        ActivityRecorder recorder,
        IConnectedNodesService connectedNodes,
        IDtmfCommandTracker dtmfTracker,
        IReflectorLinkStateService linkState,
        IRxDistortionService distortion,
        ISquelchStateService squelch,
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityRecorderHostedService> logger)
    {
        _recorder = recorder;
        _connectedNodes = connectedNodes;
        _dtmfTracker = dtmfTracker;
        _linkState = linkState;
        _distortion = distortion;
        _squelch = squelch;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RecoverOpenSessionsAsync(cancellationToken);

        _connectedNodes.OnNodeTxStarted += OnTalkerStarted;
        _connectedNodes.OnNodeTxStopped += OnTalkerStopped;
        _connectedNodes.OnReset += OnNodesReset;
        _dtmfTracker.OnDtmfCommandReceived += OnDtmfCommandReceived;
        _linkState.OnStateChanged += OnLinkStateChanged;
        _distortion.OnDistortionDetected += OnDistortionDetected;
        _squelch.OnSquelchClosed += OnSquelchClosed;

        await _recorder.RecordEventAsync(ActivityEventType.ApplicationStarted, cancellationToken: cancellationToken);

        _logger.LogInformation("ActivityRecorderHostedService démarré et abonné aux trackers d'activité");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _connectedNodes.OnNodeTxStarted -= OnTalkerStarted;
        _connectedNodes.OnNodeTxStopped -= OnTalkerStopped;
        _connectedNodes.OnReset -= OnNodesReset;
        _dtmfTracker.OnDtmfCommandReceived -= OnDtmfCommandReceived;
        _linkState.OnStateChanged -= OnLinkStateChanged;
        _distortion.OnDistortionDetected -= OnDistortionDetected;
        _squelch.OnSquelchClosed -= OnSquelchClosed;

        // Arrêt propre : la période de liaison en cours est écrite avec sa vraie durée,
        // et la session est close à l'heure exacte — pas rattrapée au démarrage suivant.
        await _recorder.RecordLinkStateAsync(ReflectorLinkState.Inactive, cancellationToken);
        await _recorder.RecordEventAsync(ActivityEventType.ApplicationStopped, cancellationToken: cancellationToken);
        await _recorder.CloseCurrentSessionAsync(cancellationToken);

        _logger.LogInformation("ActivityRecorderHostedService arrêté");
    }

    /// <summary>
    /// Referme les sessions laissées ouvertes par un arrêt brutal.
    ///
    /// La borne retenue est le dernier signe de vie enregistré, pas l'heure du redémarrage :
    /// une machine restée éteinte trois semaines ne doit pas compter trois semaines d'antenne
    /// sur le salon où elle s'était arrêtée.
    /// </summary>
    private async Task RecoverOpenSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

            var open = await repository.GetSessionsAsync(DateTimeOffset.MinValue, cancellationToken);
            if (open.All(s => !s.IsOpen))
                return;

            var lastActivity = await repository.GetLastActivityAtAsync(cancellationToken) ?? DateTimeOffset.UtcNow;

            _logger.LogWarning(
                "Historique d'activité : session(s) laissée(s) ouverte(s) par un arrêt brutal, " +
                "clôture au dernier signe de vie connu ({LastActivity:u})",
                lastActivity);

            await repository.CloseOpenSessionsAsync(lastActivity, true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Historique d'activité : échec de la clôture des sessions orphelines");
        }
    }

    private void OnTalkerStarted(ConnectedNodeInfo node)
        => _talkerStarts[node.Name] = DateTimeOffset.UtcNow;

    private async void OnTalkerStopped(ConnectedNodeInfo node)
    {
        try
        {
            if (!_talkerStarts.TryRemove(node.Name, out var startedAt))
                return;

            await _recorder.RecordEventAsync(
                ActivityEventType.TalkerHeard,
                callsign: node.Name,
                duration: DateTimeOffset.UtcNow - startedAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Historique d'activité : échec de l'enregistrement du passage de {Node}", node.Name);
        }
    }

    /// <summary>
    /// Le tracker de nœuds est remis à plat à chaque changement de salon : les passages
    /// alors en cours sont perdus, pas écrits avec une durée arbitraire.
    /// </summary>
    private void OnNodesReset() => _talkerStarts.Clear();

    private async void OnDtmfCommandReceived(string command)
    {
        try
        {
            await _recorder.RecordEventAsync(ActivityEventType.DtmfCommand, detail: command);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Historique d'activité : échec de l'enregistrement du code DTMF {Command}", command);
        }
    }

    private async void OnLinkStateChanged(ReflectorLinkState state)
    {
        try
        {
            await _recorder.RecordLinkStateAsync(state);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Historique d'activité : échec de l'enregistrement de l'état de liaison");
        }
    }

    private async void OnDistortionDetected(DateTimeOffset detectedAt)
    {
        try
        {
            await _recorder.RecordEventAsync(ActivityEventType.RxDistortion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Historique d'activité : échec de l'enregistrement d'un écrêtage");
        }
    }

    private async void OnSquelchClosed(TimeSpan duration)
    {
        try
        {
            await _recorder.RecordEventAsync(ActivityEventType.LocalTransmission, duration: duration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Historique d'activité : échec de l'enregistrement d'une réception locale");
        }
    }
}
