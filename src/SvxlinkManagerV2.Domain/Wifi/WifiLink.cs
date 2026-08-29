namespace SvxlinkManagerV2.Domain.Wifi;

/// <summary>
/// Décrit le lien réseau actif de la machine, sans déclencher de scan WiFi.
/// Destiné à la supervision : rafraîchi fréquemment, il doit rester peu coûteux.
/// </summary>
/// <param name="IsConnected">Indique qu'une interface réseau est active avec une adresse IP.</param>
/// <param name="InterfaceName">Nom de l'interface réseau (ex : wlan0), null si inconnue.</param>
/// <param name="Ssid">SSID du réseau WiFi associé (null si lien filaire ou inconnu).</param>
/// <param name="SignalPercent">Qualité du signal en pourcentage (null si non applicable).</param>
/// <param name="IpAddress">Adresse IPv4 de l'interface (null si non attribuée).</param>
public record WifiLink(
    bool IsConnected,
    string? InterfaceName,
    string? Ssid,
    int? SignalPercent,
    string? IpAddress);
