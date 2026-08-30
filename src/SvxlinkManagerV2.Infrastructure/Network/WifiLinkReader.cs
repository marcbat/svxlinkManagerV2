using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SvxlinkManagerV2.Domain.Wifi;

namespace SvxlinkManagerV2.Infrastructure.Network;

/// <summary>
/// Analyse des sources décrivant le lien réseau actif : sorties nmcli en mode terse,
/// pseudo-fichier <c>/proc/net/wireless</c> et interfaces réseau du runtime.
///
/// Isolé de <see cref="WifiService"/> pour rester testable sans processus externe.
/// </summary>
internal static class WifiLinkReader
{
    /// <summary>Chemin du pseudo-fichier exposant la qualité des liens sans fil.</summary>
    internal const string ProcNetWirelessPath = "/proc/net/wireless";

    /// <summary>Valeur maximale du champ « link » rapportée par la plupart des pilotes WiFi.</summary>
    private const double MaxLinkQuality = 70d;

    /// <summary>
    /// Découpe une ligne nmcli en mode terse, en respectant les deux-points échappés
    /// (un SSID peut contenir « : », que nmcli échappe en « \: »).
    /// </summary>
    internal static IReadOnlyList<string> SplitTerseLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '\\' && i + 1 < line.Length)
            {
                current.Append(line[++i]);
                continue;
            }

            if (c == ':')
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        fields.Add(current.ToString());
        return fields;
    }

    /// <summary>
    /// Extrait le périphérique actif de la sortie de
    /// <c>nmcli -t -f DEVICE,TYPE,STATE,CONNECTION device status</c>.
    /// Un périphérique WiFi connecté est préféré à tout autre lien.
    /// </summary>
    /// <returns>Nom du périphérique, son type et le nom de la connexion active, ou null.</returns>
    internal static (string Device, string Type, string Connection)? ParseActiveDevice(string nmcliOutput)
    {
        var candidates = new List<(string Device, string Type, string Connection)>();

        foreach (var line in nmcliOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = SplitTerseLine(line.Trim());
            if (fields.Count < 4)
                continue;

            var (device, type, state, connection) = (fields[0], fields[1], fields[2], fields[3]);

            if (!state.StartsWith("connected", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(type, "loopback", StringComparison.OrdinalIgnoreCase))
                continue;

            candidates.Add((device, type, connection));
        }

        if (candidates.Count == 0)
            return null;

        var wifi = candidates.FirstOrDefault(c => c.Type.Contains("wifi", StringComparison.OrdinalIgnoreCase));
        return wifi.Device is null ? candidates[0] : wifi;
    }

    /// <summary>
    /// Extrait la première adresse IPv4 de la sortie de
    /// <c>nmcli -t -f IP4.ADDRESS device show &lt;device&gt;</c>.
    /// </summary>
    internal static string? ParseIpAddress(string nmcliOutput)
    {
        foreach (var line in nmcliOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = SplitTerseLine(line.Trim());
            if (fields.Count < 2)
                continue;

            if (!fields[0].StartsWith("IP4.ADDRESS", StringComparison.OrdinalIgnoreCase))
                continue;

            // Format « adresse/préfixe » : seule l'adresse est affichée.
            var value = fields[1].Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var slashIndex = value.IndexOf('/');
            return slashIndex >= 0 ? value[..slashIndex] : value;
        }

        return null;
    }

    /// <summary>
    /// Extrait la qualité du lien d'un périphérique depuis le contenu de
    /// <c>/proc/net/wireless</c> et la convertit en pourcentage.
    /// </summary>
    internal static int? ParseSignalPercent(string procNetWirelessContent, string device)
    {
        foreach (var line in procNetWirelessContent.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            var separatorIndex = trimmed.IndexOf(':');
            if (separatorIndex <= 0)
                continue;

            var interfaceName = trimmed[..separatorIndex].Trim();
            if (!string.Equals(interfaceName, device, StringComparison.Ordinal))
                continue;

            // Colonnes après « wlan0: » : status, link, level, noise, ...
            var fields = trimmed[(separatorIndex + 1)..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (fields.Length < 2)
                return null;

            // Les valeurs sont suffixées d'un point (« 70. ») et parfois négatives.
            var linkField = fields[1].TrimEnd('.');
            if (!double.TryParse(linkField, NumberStyles.Float, CultureInfo.InvariantCulture, out var link))
                return null;

            var percent = (int)Math.Round(Math.Clamp(link / MaxLinkQuality * 100d, 0d, 100d));
            return percent;
        }

        return null;
    }

    /// <summary>
    /// Détermine le lien actif à partir des seules interfaces réseau du runtime.
    /// Utilisé lorsque nmcli est indisponible (conteneur Docker, poste de développement) :
    /// l'adresse IP reste connue même sans NetworkManager.
    /// </summary>
    internal static WifiLink ReadFromRuntimeInterfaces()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            // Une interface sans fil est préférée : c'est le lien supervisé sur la cible.
            var selected = candidates.FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                ?? candidates.FirstOrDefault();

            if (selected is null)
                return new WifiLink(false, null, null, null, null);

            var ipAddress = selected.GetIPProperties().UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?
                .Address.ToString();

            return new WifiLink(
                IsConnected: ipAddress is not null,
                InterfaceName: selected.Name,
                Ssid: null,
                SignalPercent: null,
                IpAddress: ipAddress);
        }
        catch (NetworkInformationException)
        {
            return new WifiLink(false, null, null, null, null);
        }
    }
}
