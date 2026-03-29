using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Tracker des nœuds connectés au réflecteur SVXLink.
/// Parse les logs SVXLink en temps réel pour détecter les connexions/déconnexions.
/// Thread-safe, singleton.
/// </summary>
public class ConnectedNodesTracker : IConnectedNodesService, IDisposable
{
    private readonly ILogger<ConnectedNodesTracker> _logger;
    private readonly ISvxLinkLogService _logService;
    private readonly HashSet<string> _nodes = new();
    private readonly HashSet<string> _txNodes = new();
    private readonly object _lock = new();
    private bool _disposed;

    public event Action<ConnectedNodeInfo>? OnNodeJoined;
    public event Action<ConnectedNodeInfo>? OnNodeLeft;
    public event Action<IReadOnlyList<ConnectedNodeInfo>>? OnNodesInitialized;
    public event Action<ConnectedNodeInfo>? OnNodeTxStarted;
    public event Action<ConnectedNodeInfo>? OnNodeTxStopped;

    public IReadOnlyList<ConnectedNodeInfo> ConnectedNodes
    {
        get
        {
            lock (_lock)
            {
                return _nodes.Select(name => new ConnectedNodeInfo(name, _txNodes.Contains(name))).ToList().AsReadOnly();
            }
        }
    }

    public ConnectedNodesTracker(
        ILogger<ConnectedNodesTracker> logger,
        ISvxLinkLogService logService)
    {
        _logger = logger;
        _logService = logService;

        // S'abonner aux logs SVXLink pour parser les connexions/déconnexions
        _logService.OnLogReceived += OnLogReceived;

        _logger.LogInformation("ConnectedNodesTracker initialisé et abonné aux logs SVXLink");
    }

    private void OnLogReceived(SvxLinkLogEntry entry)
    {
        var message = entry.Message;

        // Pattern 1: "ReflectorLogic: Connected nodes: HB9GXP2-H, HB9GXP-H"
        // → Initialisation de la liste des nœuds connectés
        if (message.Contains("Connected nodes:", StringComparison.OrdinalIgnoreCase))
        {
            ProcessConnectedNodesLine(message);
            return;
        }

        // Pattern 2: "ReflectorLogic: Node joined: HB9GXP2-H"
        // → Un nœud a rejoint le salon
        if (message.Contains("Node joined:", StringComparison.OrdinalIgnoreCase))
        {
            ProcessNodeJoinedLine(message);
            return;
        }

        // Pattern 3: "ReflectorLogic: Node left: HB9GXP2-H"
        // → Un nœud a quitté le salon
        if (message.Contains("Node left:", StringComparison.OrdinalIgnoreCase))
        {
            ProcessNodeLeftLine(message);
            return;
        }

        // Pattern 4: "ReflectorLogic: Talker start: HB9GXP2-H"
        // → Un nœud commence à émettre (TX)
        if (message.Contains("Talker start:", StringComparison.OrdinalIgnoreCase))
        {
            ProcessTalkerStartLine(message);
            return;
        }

        // Pattern 5: "ReflectorLogic: Talker stop: HB9GXP2-H"
        // → Un nœud arrête d'émettre (TX)
        if (message.Contains("Talker stop:", StringComparison.OrdinalIgnoreCase))
        {
            ProcessTalkerStopLine(message);
            return;
        }
    }

    private void ProcessConnectedNodesLine(string message)
    {
        try
        {
            // Format: "ReflectorLogic: Connected nodes: HB9GXP2-H, HB9GXP-H"
            // Split par ':' et prendre la partie après "Connected nodes:"
            var parts = message.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
            {
                _logger.LogWarning("Format inattendu pour 'Connected nodes': {Message}", message);
                return;
            }

            // parts[0] = "ReflectorLogic"
            // parts[1] = "Connected nodes"
            // parts[2] = "HB9GXP2-H, HB9GXP-H"
            var nodesString = parts[2];
            var nodeNames = nodesString
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            lock (_lock)
            {
                _nodes.Clear();
                foreach (var nodeName in nodeNames)
                {
                    _nodes.Add(nodeName);
                }
            }

            var connectedNodesList = nodeNames.Select(n => new ConnectedNodeInfo(n)).ToList().AsReadOnly();
            _logger.LogInformation("Liste de nœuds connectés initialisée : {Count} nœud(s)", connectedNodesList.Count);

            OnNodesInitialized?.Invoke(connectedNodesList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du parsing de 'Connected nodes': {Message}", message);
        }
    }

    private void ProcessNodeJoinedLine(string message)
    {
        try
        {
            // Format: "ReflectorLogic: Node joined: HB9GXP2-H"
            var parts = message.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
            {
                _logger.LogWarning("Format inattendu pour 'Node joined': {Message}", message);
                return;
            }

            var nodeName = parts[2].Trim();
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                _logger.LogWarning("Nom de nœud vide dans 'Node joined': {Message}", message);
                return;
            }

            bool wasAdded;
            lock (_lock)
            {
                wasAdded = _nodes.Add(nodeName);
            }

            if (wasAdded)
            {
                _logger.LogInformation("Nœud connecté : {NodeName}", nodeName);
                OnNodeJoined?.Invoke(new ConnectedNodeInfo(nodeName));
            }
            else
            {
                _logger.LogDebug("Nœud déjà connecté (doublon ignoré) : {NodeName}", nodeName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du parsing de 'Node joined': {Message}", message);
        }
    }

    private void ProcessNodeLeftLine(string message)
    {
        try
        {
            // Format: "ReflectorLogic: Node left: HB9GXP2-H"
            var parts = message.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
            {
                _logger.LogWarning("Format inattendu pour 'Node left': {Message}", message);
                return;
            }

            var nodeName = parts[2].Trim();
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                _logger.LogWarning("Nom de nœud vide dans 'Node left': {Message}", message);
                return;
            }

            bool wasRemoved;
            lock (_lock)
            {
                wasRemoved = _nodes.Remove(nodeName);
                _txNodes.Remove(nodeName);
            }

            if (wasRemoved)
            {
                _logger.LogInformation("Nœud déconnecté : {NodeName}", nodeName);
                OnNodeLeft?.Invoke(new ConnectedNodeInfo(nodeName));
            }
            else
            {
                _logger.LogDebug("Nœud non trouvé lors de la déconnexion : {NodeName}", nodeName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du parsing de 'Node left': {Message}", message);
        }
    }

    private bool TryExtractNodeName(string message, string context, out string nodeName)
    {
        nodeName = string.Empty;
        var parts = message.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            _logger.LogWarning("Format inattendu pour '{Context}': {Message}", context, message);
            return false;
        }

        nodeName = parts[2].Trim();
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            _logger.LogWarning("Nom de nœud vide dans '{Context}': {Message}", context, message);
            return false;
        }

        return true;
    }

    private void ProcessTalkerStartLine(string message)
    {
        try
        {
            // Format: "ReflectorLogic: Talker start: HB9GXP2-H"
            if (!TryExtractNodeName(message, "Talker start", out var nodeName))
                return;

            bool nodeExists;
            lock (_lock)
            {
                nodeExists = _nodes.Contains(nodeName);
                if (nodeExists)
                {
                    _txNodes.Add(nodeName);
                }
            }

            if (nodeExists)
            {
                _logger.LogInformation("Nœud en émission (TX start) : {NodeName}", nodeName);
                OnNodeTxStarted?.Invoke(new ConnectedNodeInfo(nodeName, true));
            }
            else
            {
                _logger.LogWarning("TX start reçu pour un nœud absent de la liste : {NodeName}", nodeName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du parsing de 'Talker start': {Message}", message);
        }
    }

    private void ProcessTalkerStopLine(string message)
    {
        try
        {
            // Format: "ReflectorLogic: Talker stop: HB9GXP2-H"
            if (!TryExtractNodeName(message, "Talker stop", out var nodeName))
                return;

            bool wasTransmitting;
            lock (_lock)
            {
                wasTransmitting = _txNodes.Remove(nodeName);
            }

            if (wasTransmitting)
            {
                _logger.LogInformation("Nœud arrête l'émission (TX stop) : {NodeName}", nodeName);
                OnNodeTxStopped?.Invoke(new ConnectedNodeInfo(nodeName, false));
            }
            else
            {
                _logger.LogDebug("TX stop reçu pour un nœud non en émission : {NodeName}", nodeName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du parsing de 'Talker stop': {Message}", message);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _nodes.Clear();
            _txNodes.Clear();
        }

        _logger.LogInformation("ConnectedNodesTracker réinitialisé - liste des nœuds vidée");
        OnNodesInitialized?.Invoke(Array.Empty<ConnectedNodeInfo>());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _logService.OnLogReceived -= OnLogReceived;
        _logger.LogInformation("ConnectedNodesTracker dispose - désabonnement des logs SVXLink");

        _disposed = true;
    }
}
