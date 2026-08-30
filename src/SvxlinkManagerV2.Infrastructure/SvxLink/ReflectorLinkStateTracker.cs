using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Tracker de l'état de la liaison au réflecteur SVXLink.
/// Parse en temps réel les lignes émises par la logique <c>ReflectorLogic</c> pour
/// distinguer l'état de la liaison de celui du processus svxlink.
/// Thread-safe, singleton.
///
/// Motifs reconnus (identiques en 19.09.2 et 25.05 sauf mention contraire) :
///   - "ReflectorLogic: Connecting to HOTE:PORT" — "Connecting to service SERVICE" en 25.05
///   - "ReflectorLogic: Connection established to ..." : TCP établi, liaison pas encore montée
///   - "ReflectorLogic: Encrypted connection established" : TLS établi (25.05 uniquement)
///   - "ReflectorLogic: Authentication OK" : authentification acceptée
///   - "ReflectorLogic: Connected nodes: ..." : liaison établie
///   - "ReflectorLogic: Error message received from server: ..." : refus du réflecteur (19.09.2)
///   - "*** ERROR[ReflectorLogic]: Server error: ..." : refus du réflecteur (25.05)
///   - "ReflectorLogic: Disconnected from HOTE:PORT: CAUSE" : liaison coupée
///   - "ReflectorLogic: Heartbeat timeout" : liaison perdue
///   - messages liés au certificat client : certificat rejeté (25.05 uniquement)
///   - "*** ERROR: ReflectorLogic/HOST missing in configuration" : configuration incomplète
/// </summary>
public class ReflectorLinkStateTracker : IReflectorLinkStateService, IDisposable
{
    /// <summary>Nom de la logique réflecteur dans svxlink.conf : préfixe de toutes les lignes exploitées.</summary>
    private const string ReflectorLogicName = "ReflectorLogic";

    private readonly ILogger<ReflectorLinkStateTracker> _logger;
    private readonly ISvxLinkLogService _logService;
    private readonly object _lock = new();
    private ReflectorLinkState _state = ReflectorLinkState.Inactive;
    private bool _disposed;

    public event Action<ReflectorLinkState>? OnStateChanged;

    public ReflectorLinkState State
    {
        get
        {
            lock (_lock) return _state;
        }
    }

    public ReflectorLinkStateTracker(
        ILogger<ReflectorLinkStateTracker> logger,
        ISvxLinkLogService logService)
    {
        _logger = logger;
        _logService = logService;

        _logService.OnLogReceived += OnLogReceived;

        _logger.LogInformation("ReflectorLinkStateTracker initialisé et abonné aux logs SVXLink");
    }

    public void BeginConnecting() =>
        Publish(new ReflectorLinkState(ReflectorLinkStatus.Connecting));

    public void MarkNotApplicable() =>
        Publish(ReflectorLinkState.NotApplicable);

    private void OnLogReceived(SvxLinkLogEntry entry)
    {
        try
        {
            ReflectorLinkState? next;
            lock (_lock)
            {
                // Mode autonome : aucune liaison n'est attendue, les lignes résiduelles
                // du daemon ne doivent pas faire apparaître une liaison en erreur.
                if (_state.Status == ReflectorLinkStatus.NotApplicable)
                    return;

                next = Interpret(entry.Message, _state);
            }

            if (next is not null)
                Publish(next);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'analyse de l'état de liaison : {Message}", entry.Message);
        }
    }

    private void Publish(ReflectorLinkState next)
    {
        lock (_lock)
        {
            if (_state == next)
                return;

            _state = next;
        }

        if (next.IsFaulted)
            _logger.LogWarning(
                "Liaison réflecteur {Status} ({Reason}) : {Detail}",
                next.Status, next.Reason, next.Detail ?? "aucun détail");
        else
            _logger.LogInformation("Liaison réflecteur : {Status}", next.Status);

        OnStateChanged?.Invoke(next);
    }

    /// <summary>
    /// Déduit le nouvel état de liaison d'une ligne de log SVXLink.
    /// Retourne <c>null</c> quand la ligne ne décrit aucune transition.
    /// </summary>
    /// <param name="message">Ligne de log brute émise par SVXLink.</param>
    /// <param name="current">État courant, nécessaire pour distinguer un échec d'une perte de liaison.</param>
    internal static ReflectorLinkState? Interpret(string message, ReflectorLinkState current)
    {
        if (string.IsNullOrWhiteSpace(message) ||
            !message.Contains(ReflectorLogicName, StringComparison.OrdinalIgnoreCase))
            return null;

        var detail = message.Trim();

        // Configuration incomplète : SVXLink refuse de monter la logique réflecteur.
        if (Has(message, "missing in configuration"))
            return new ReflectorLinkState(ReflectorLinkStatus.Failed, ReflectorLinkFailureReason.ConfigurationInvalid, detail);

        // Certificat client du protocole V3 (25.05) rejeté, illisible ou absent.
        if (IsCertificateFailure(message))
            return new ReflectorLinkState(ReflectorLinkStatus.Failed, ReflectorLinkFailureReason.CertificateRejected, detail);

        // Refus explicite du réflecteur, transmis dans un message de protocole MsgError.
        if (TryExtractServerError(message, out var serverError))
            return new ReflectorLinkState(ReflectorLinkStatus.Failed, ClassifyServerError(serverError), detail);

        // Le réflecteur a transmis la liste des nœuds : la liaison est pleinement établie.
        if (Has(message, "Connected nodes:"))
            return new ReflectorLinkState(ReflectorLinkStatus.Connected);

        // Authentification acceptée : la liaison monte, l'éventuel échec précédent est caduc.
        if (Has(message, "Authentication OK"))
            return current.Status == ReflectorLinkStatus.Connected
                ? null
                : new ReflectorLinkState(ReflectorLinkStatus.Connecting);

        // Plus de battement de cœur : la liaison est perdue même si le socket n'est pas encore fermé.
        if (Has(message, "Heartbeat timeout"))
            return new ReflectorLinkState(Lost(current), ReflectorLinkFailureReason.HeartbeatTimeout, detail);

        if (Has(message, "Disconnected from"))
        {
            // Une déconnexion suit toujours un refus du réflecteur : la cause déjà
            // identifiée est plus informative que "Locally ordered disconnect".
            if (current.Status == ReflectorLinkStatus.Failed)
                return null;

            return new ReflectorLinkState(Lost(current), ClassifyDisconnectReason(message), detail);
        }

        // Nouvelle tentative : la cause du dernier échec est conservée pour rester
        // visible pendant les reconnexions automatiques de SVXLink.
        if (Has(message, "Connecting to") ||
            Has(message, "Connection established to") ||
            Has(message, "Encrypted connection established"))
            return new ReflectorLinkState(ReflectorLinkStatus.Connecting, current.Reason, current.Detail);

        return null;
    }

    /// <summary>
    /// Une liaison qui était établie est perdue ; sinon elle n'a jamais abouti.
    /// </summary>
    private static ReflectorLinkStatus Lost(ReflectorLinkState current) =>
        current.Status == ReflectorLinkStatus.Connected
            ? ReflectorLinkStatus.Disconnected
            : ReflectorLinkStatus.Failed;

    private static bool IsCertificateFailure(string message) =>
        Has(message, "certificate") &&
        (Has(message, "Failed to load client certificate") ||
         Has(message, "does not match our current") ||
         Has(message, "Received an empty certificate") ||
         Has(message, "Failed to parse certificate") ||
         Has(message, "Failed to write certificate"));

    /// <summary>
    /// Extrait le message d'erreur envoyé par le réflecteur.
    /// 19.09.2 : "ReflectorLogic: Error message received from server: MESSAGE".
    /// 25.05 : "*** ERROR[ReflectorLogic]: Server error: MESSAGE".
    /// </summary>
    private static bool TryExtractServerError(string message, out string serverError)
    {
        string[] markers = ["Error message received from server:", "Server error:"];

        foreach (var marker in markers)
        {
            var index = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;

            serverError = message[(index + marker.Length)..].Trim();
            return true;
        }

        serverError = string.Empty;
        return false;
    }

    private static ReflectorLinkFailureReason ClassifyServerError(string serverError)
    {
        if (Has(serverError, "Access denied") ||
            Has(serverError, "Not authorized") ||
            Has(serverError, "Invalid callsign") ||
            Has(serverError, "Unknown user") ||
            Has(serverError, "Authentication"))
            return ReflectorLinkFailureReason.AuthenticationRejected;

        if (Has(serverError, "Protocol"))
            return ReflectorLinkFailureReason.ProtocolError;

        return ReflectorLinkFailureReason.Unknown;
    }

    /// <summary>
    /// Classe la cause de déconnexion produite par <c>TcpConnection::disconnectReasonStr</c>
    /// ou, pour les erreurs système, par <c>strerror(errno)</c>.
    /// </summary>
    private static ReflectorLinkFailureReason ClassifyDisconnectReason(string message)
    {
        if (Has(message, "Host not found") ||
            Has(message, "Name or service not known") ||
            Has(message, "Connection refused") ||
            Has(message, "No route to host") ||
            Has(message, "Network is unreachable") ||
            Has(message, "timed out"))
            return ReflectorLinkFailureReason.HostUnreachable;

        if (Has(message, "Protocol error") || Has(message, "bad state"))
            return ReflectorLinkFailureReason.ProtocolError;

        if (Has(message, "Connection closed by remote peer"))
            return ReflectorLinkFailureReason.RemoteDisconnected;

        return ReflectorLinkFailureReason.Unknown;
    }

    private static bool Has(string message, string pattern) =>
        message.Contains(pattern, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (_disposed)
            return;

        _logService.OnLogReceived -= OnLogReceived;
        _logger.LogInformation("ReflectorLinkStateTracker dispose - désabonnement des logs SVXLink");

        _disposed = true;
    }
}
