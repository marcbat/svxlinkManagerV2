using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service hébergé qui surveille les logs SVXLink après le démarrage du daemon
/// et joue l'annonce sonore appropriée selon le résultat de la connexion au réflecteur.
///
/// Comportement :
///   - Armé par l'événement OnReset de IConnectedNodesService (avant chaque redémarrage SVXLink)
///   - Sur "Connected nodes:" (via OnNodesInitialized) → joue Name.wav via commande DTMF 398
///   - Sur erreur d'autorisation dans les logs → génère et joue une annonce TTS d'échec via commande DTMF 399
///
/// Remplace l'annonce systématique de proc startup {} dans Logic.tcl qui se déclenchait
/// sans vérifier si la connexion au réflecteur était réellement établie.
/// </summary>
public class ReflectorConnectionAnnouncementService : IHostedService, IDisposable
{
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly ISvxLinkLogService _logService;
    private readonly ITtsService _ttsService;
    private readonly IDtmfPtyWriter _ptyWriter;
    private readonly ILogger<ReflectorConnectionAnnouncementService> _logger;

    private bool _disposed;

    // 0 = non armé, 1 = armé (en attente d'une confirmation de connexion)
    private int _isPendingConnection;

    /// <summary>Commande DTMF interne pour jouer l'annonce de connexion réussie (Name.wav).</summary>
    internal const int SuccessAnnouncementDtmfCode = 398;

    /// <summary>Commande DTMF interne pour jouer le fichier TTS généré par .NET.</summary>
    internal const int TtsPlaybackDtmfCode = 399;

    /// <summary>Chemin du fichier WAV temporaire pour les annonces TTS.</summary>
    internal const string TtsWavPath = "/tmp/svxlink_tts.wav";

    public ReflectorConnectionAnnouncementService(
        IConnectedNodesService connectedNodesService,
        ISvxLinkLogService logService,
        ITtsService ttsService,
        IDtmfPtyWriter ptyWriter,
        ILogger<ReflectorConnectionAnnouncementService> logger)
    {
        _connectedNodesService = connectedNodesService;
        _logService = logService;
        _ttsService = ttsService;
        _ptyWriter = ptyWriter;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _connectedNodesService.OnReset += OnConnectionReset;
        _connectedNodesService.OnNodesInitialized += OnNodesInitialized;
        _logService.OnLogReceived += OnLogReceived;

        _logger.LogInformation("ReflectorConnectionAnnouncementService démarré");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _connectedNodesService.OnReset -= OnConnectionReset;
        _connectedNodesService.OnNodesInitialized -= OnNodesInitialized;
        _logService.OnLogReceived -= OnLogReceived;

        _logger.LogInformation("ReflectorConnectionAnnouncementService arrêté");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Déclenché avant chaque redémarrage du daemon SVXLink.
    /// Arme le service pour surveiller la prochaine tentative de connexion.
    /// </summary>
    internal void OnConnectionReset()
    {
        Interlocked.Exchange(ref _isPendingConnection, 1);
        _logger.LogInformation("Service d'annonce de connexion armé — en attente de confirmation du réflecteur");
    }

    /// <summary>
    /// Déclenché quand SVXLink reçoit le message "Connected nodes:" du réflecteur.
    /// Joue l'annonce de connexion réussie si le service est armé.
    /// </summary>
    internal async void OnNodesInitialized(IReadOnlyList<ConnectedNodeInfo> nodes)
    {
        try
        {
            if (Interlocked.CompareExchange(ref _isPendingConnection, 0, 1) != 1)
                return;

            _logger.LogInformation(
                "Connexion réussie au réflecteur ({Count} nœud(s)) — déclenchement de l'annonce sonore",
                nodes.Count);

            var result = await _ptyWriter.SendCommandAsync(SuccessAnnouncementDtmfCode.ToString());
            result.Match(
                Succ: _ => { return LanguageExt.Unit.Default; },
                Fail: errors =>
                {
                    _logger.LogWarning(
                        "Échec de l'envoi de l'annonce de connexion réussie : {Errors}",
                        string.Join(", ", errors.Select(e => e.Message)));
                    return LanguageExt.Unit.Default;
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du traitement de l'annonce de connexion réussie");
        }
    }

    /// <summary>
    /// Déclenché à chaque nouvelle ligne de log SVXLink.
    /// Détecte les erreurs d'autorisation et joue une annonce d'échec si le service est armé.
    /// </summary>
    internal async void OnLogReceived(SvxLinkLogEntry entry)
    {
        try
        {
            if (Interlocked.CompareExchange(ref _isPendingConnection, 0, 0) == 0)
                return;

            if (!IsConnectionFailureMessage(entry.Message))
                return;

            if (Interlocked.CompareExchange(ref _isPendingConnection, 0, 1) != 1)
                return;

            _logger.LogWarning("Échec de connexion au réflecteur détecté dans les logs : {Message}", entry.Message);

            var failureText = "Connexion au réflecteur échouée. Vérifiez la configuration et les autorisations.";
            var ttsResult = await _ttsService.GenerateWavAsync(failureText, TtsWavPath);
            if (ttsResult.IsFail)
            {
                _logger.LogWarning("Échec de la génération TTS pour l'annonce d'échec de connexion");
                return;
            }

            var ptyResult = await _ptyWriter.SendCommandAsync(TtsPlaybackDtmfCode.ToString());
            ptyResult.Match(
                Succ: _ => { return LanguageExt.Unit.Default; },
                Fail: errors =>
                {
                    _logger.LogWarning(
                        "Échec de l'envoi de l'annonce d'échec de connexion : {Errors}",
                        string.Join(", ", errors.Select(e => e.Message)));
                    return LanguageExt.Unit.Default;
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du traitement de l'annonce d'échec de connexion");
        }
    }

    /// <summary>
    /// Détermine si un message de log indique un échec d'autorisation ou de connexion au réflecteur.
    /// </summary>
    internal static bool IsConnectionFailureMessage(string message) =>
        message.Contains("Access denied", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Not authorized", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (_disposed)
            return;

        _connectedNodesService.OnReset -= OnConnectionReset;
        _connectedNodesService.OnNodesInitialized -= OnNodesInitialized;
        _logService.OnLogReceived -= OnLogReceived;

        _logger.LogInformation("ReflectorConnectionAnnouncementService dispose - désabonnement");
        _disposed = true;
    }
}
