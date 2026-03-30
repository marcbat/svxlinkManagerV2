using LanguageExt;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Wifi;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Interface du service de gestion WiFi.
/// Pilote NetworkManager via nmcli pour scanner, connecter et gérer les profils WiFi.
/// Les données sont transientes (pas de persistance DB).
/// </summary>
public interface IWifiService
{
    /// <summary>
    /// Scanne les réseaux WiFi disponibles.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Liste des réseaux détectés</returns>
    Task<Validation<Error, IReadOnlyList<WifiNetwork>>> ScanNetworksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère les connexions WiFi sauvegardées dans NetworkManager.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Liste des connexions sauvegardées</returns>
    Task<Validation<Error, IReadOnlyList<WifiConnection>>> GetSavedConnectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Connecte à un réseau WiFi avec le mot de passe fourni.
    /// </summary>
    /// <param name="ssid">SSID du réseau cible</param>
    /// <param name="password">Mot de passe WPA2</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Succès ou erreur</returns>
    Task<Validation<Error, Unit>> ConnectAsync(string ssid, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active une connexion WiFi existante via son UUID NetworkManager.
    /// </summary>
    /// <param name="uuid">UUID de la connexion NetworkManager</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Succès ou erreur</returns>
    Task<Validation<Error, Unit>> ActivateConnectionAsync(string uuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Désactive une connexion WiFi active via son UUID NetworkManager.
    /// </summary>
    /// <param name="uuid">UUID de la connexion NetworkManager</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Succès ou erreur</returns>
    Task<Validation<Error, Unit>> DeactivateConnectionAsync(string uuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime un profil de connexion WiFi de NetworkManager.
    /// </summary>
    /// <param name="uuid">UUID de la connexion NetworkManager à supprimer</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Succès ou erreur</returns>
    Task<Validation<Error, Unit>> DeleteConnectionAsync(string uuid, CancellationToken cancellationToken = default);
}
