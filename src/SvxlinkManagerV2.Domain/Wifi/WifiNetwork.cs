namespace SvxlinkManagerV2.Domain.Wifi;

/// <summary>
/// Représente un réseau WiFi détecté lors d'un scan.
/// Données transientes (pas de persistance DB).
/// </summary>
/// <param name="InUse">Indique si ce réseau est actuellement utilisé (connexion active)</param>
/// <param name="Ssid">Identifiant du réseau WiFi</param>
/// <param name="Mode">Mode du réseau (Infra, Ad-Hoc, etc.)</param>
/// <param name="Channel">Canal WiFi utilisé</param>
/// <param name="Rate">Débit maximum affiché par nmcli</param>
/// <param name="Signal">Niveau de signal en pourcentage (0-100)</param>
/// <param name="Bars">Représentation graphique du signal (0-4 barres)</param>
/// <param name="Security">Type de sécurité (WPA2, WPA1, etc.)</param>
/// <param name="HasSavedProfile">Indique si un profil NetworkManager existe pour ce réseau</param>
/// <param name="ConnectionUuid">UUID de la connexion NetworkManager sauvegardée (si HasSavedProfile = true)</param>
public record WifiNetwork(
    bool InUse,
    string Ssid,
    string Mode,
    string Channel,
    string Rate,
    int Signal,
    int Bars,
    string Security,
    bool HasSavedProfile,
    string? ConnectionUuid);
