using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de suivi des nœuds connectés au réflecteur SVXLink.
/// Expose la liste des nœuds connectés et des événements pour les connexions/déconnexions.
/// </summary>
public interface IConnectedNodesService
{
    /// <summary>
    /// Liste des nœuds actuellement connectés au réflecteur.
    /// </summary>
    IReadOnlyList<ConnectedNodeInfo> ConnectedNodes { get; }

    /// <summary>
    /// Événement déclenché quand un nœud rejoint le salon (après un "Node joined" dans les logs).
    /// </summary>
    event Action<ConnectedNodeInfo>? OnNodeJoined;

    /// <summary>
    /// Événement déclenché quand un nœud quitte le salon (après un "Node left" dans les logs).
    /// </summary>
    event Action<ConnectedNodeInfo>? OnNodeLeft;

    /// <summary>
    /// Événement déclenché lors de l'initialisation de la liste des nœuds
    /// (après un "Connected nodes: ..." dans les logs).
    /// </summary>
    event Action<IReadOnlyList<ConnectedNodeInfo>>? OnNodesInitialized;

    /// <summary>
    /// Événement déclenché quand un nœud commence à émettre (après un "Talker start" dans les logs).
    /// </summary>
    event Action<ConnectedNodeInfo>? OnNodeTxStarted;

    /// <summary>
    /// Événement déclenché quand un nœud arrête d'émettre (après un "Talker stop" dans les logs).
    /// </summary>
    event Action<ConnectedNodeInfo>? OnNodeTxStopped;

    /// <summary>
    /// Événement déclenché lors de la réinitialisation de la liste des nœuds
    /// (appelé avant chaque redémarrage du daemon SVXLink).
    /// Sert à armer les services en attente de confirmation de connexion.
    /// </summary>
    event Action? OnReset;

    /// <summary>
    /// Vide la liste des nœuds connectés (appelé lors de la déconnexion du salon).
    /// Déclenche OnNodesInitialized avec une liste vide pour mettre à jour l'UI.
    /// </summary>
    void Reset();
}
