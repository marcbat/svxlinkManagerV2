namespace SvxlinkManagerV2.Domain.Wifi;

/// <summary>
/// Représente l'état WiFi courant du système, incluant la connexion active et les réseaux disponibles.
/// </summary>
/// <param name="IsConnected">Indique si une connexion WiFi est actuellement active</param>
/// <param name="ConnectedSsid">SSID du réseau connecté (null si déconnecté)</param>
/// <param name="Signal">Niveau de signal du réseau connecté en pourcentage (null si déconnecté)</param>
/// <param name="Networks">Liste des réseaux WiFi détectés lors du dernier scan</param>
public record WifiStatus(
    bool IsConnected,
    string? ConnectedSsid,
    int? Signal,
    IReadOnlyList<WifiNetwork> Networks);
