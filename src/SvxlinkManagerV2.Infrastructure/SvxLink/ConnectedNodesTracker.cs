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
    private readonly object _lock = new();
    private bool _disposed;

    public event Action<ConnectedNodeInfo>? OnNodeJoined;
    public event Action<ConnectedNodeInfo>? OnNodeLeft;
    public event Action<IReadOnlyList<ConnectedNodeInfo>>? OnNodesInitialized;

    public IReadOnlyList<ConnectedNodeInfo> ConnectedNodes
    {
        get
        {
            lock (_lock)
            {
                return _nodes.Select(name => new ConnectedNodeInfo(name)).ToList().AsReadOnly();
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

    public void Reset()
    {
        lock (_lock)
        {
            _nodes.Clear();
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
