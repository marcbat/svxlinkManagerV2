namespace SvxlinkManagerV2.Domain.Wifi;

/// <summary>
/// Représente une connexion WiFi sauvegardée dans NetworkManager.
/// </summary>
/// <param name="Name">Nom de la connexion (généralement le SSID)</param>
/// <param name="Uuid">Identifiant unique de la connexion NetworkManager</param>
/// <param name="Type">Type de connexion (802-11-wireless)</param>
/// <param name="Device">Périphérique réseau associé (ou vide si inactif)</param>
public record WifiConnection(
    string Name,
    string Uuid,
    string Type,
    string Device);
