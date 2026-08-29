using LanguageExt;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Wifi;
using System.Diagnostics;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Infrastructure.Network;

/// <summary>
/// Implémentation du service WiFi utilisant NetworkManager (nmcli) sur le système hôte.
/// Utilisé en production sur Orange Pi/Armbian.
/// </summary>
public class WifiService : IWifiService
{
    private readonly ILogger<WifiService> _logger;
    private readonly string _procNetWirelessPath;

    public WifiService(ILogger<WifiService> logger, string? procNetWirelessPath = null)
    {
        _logger = logger;
        _procNetWirelessPath = procNetWirelessPath ?? WifiLinkReader.ProcNetWirelessPath;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, IReadOnlyList<WifiNetwork>>> ScanNetworksAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scan des réseaux WiFi via nmcli");

        var result = await RunNmcliAsync(
            new[] { "-f", "IN-USE,SSID,MODE,CHAN,RATE,SIGNAL,BARS,SECURITY", "device", "wifi", "list" },
            cancellationToken);

        return result.Match(
            Succ: output =>
            {
                var networks = ParseNetworksOutput(output);
                _logger.LogInformation("Scan WiFi : {Count} réseau(x) détecté(s)", networks.Count);
                return Validation<Error, IReadOnlyList<WifiNetwork>>.Success(networks);
            },
            Fail: errors => Validation<Error, IReadOnlyList<WifiNetwork>>.Fail(errors));
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, IReadOnlyList<WifiConnection>>> GetSavedConnectionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Récupération des connexions WiFi sauvegardées via nmcli");

        var result = await RunNmcliAsync(
            new[] { "-f", "NAME,UUID,TYPE,DEVICE", "c" },
            cancellationToken);

        return result.Match(
            Succ: output =>
            {
                var connections = ParseConnectionsOutput(output);
                _logger.LogInformation("Connexions WiFi sauvegardées : {Count}", connections.Count);
                return Validation<Error, IReadOnlyList<WifiConnection>>.Success(connections);
            },
            Fail: errors => Validation<Error, IReadOnlyList<WifiConnection>>.Fail(errors));
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, Unit>> ConnectAsync(string ssid, string password, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connexion au réseau WiFi : {Ssid}", ssid);
        // Le mot de passe n'est jamais loggé — passé directement au processus sans interprétation shell

        var result = await RunNmcliAsync(
            new[] { "d", "wifi", "connect", ssid, "password", password },
            cancellationToken);

        return result.Match(
            Succ: _ =>
            {
                _logger.LogInformation("Connexion WiFi réussie : {Ssid}", ssid);
                return Validation<Error, Unit>.Success(Unit.Default);
            },
            Fail: errors =>
            {
                _logger.LogWarning("Échec de connexion WiFi : {Ssid}", ssid);
                return Validation<Error, Unit>.Fail(errors);
            });
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, Unit>> ActivateConnectionAsync(string uuid, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Activation de la connexion WiFi : {Uuid}", uuid);

        var result = await RunNmcliAsync(new[] { "connection", "up", uuid }, cancellationToken);

        return result.Match(
            Succ: _ =>
            {
                _logger.LogInformation("Activation WiFi réussie : {Uuid}", uuid);
                return Validation<Error, Unit>.Success(Unit.Default);
            },
            Fail: errors =>
            {
                _logger.LogWarning("Échec d'activation WiFi : {Uuid}", uuid);
                return Validation<Error, Unit>.Fail(errors);
            });
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, Unit>> DeactivateConnectionAsync(string uuid, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Désactivation de la connexion WiFi : {Uuid}", uuid);

        var result = await RunNmcliAsync(new[] { "connection", "down", uuid }, cancellationToken);

        return result.Match(
            Succ: _ =>
            {
                _logger.LogInformation("Désactivation WiFi réussie : {Uuid}", uuid);
                return Validation<Error, Unit>.Success(Unit.Default);
            },
            Fail: errors =>
            {
                _logger.LogWarning("Échec de désactivation WiFi : {Uuid}", uuid);
                return Validation<Error, Unit>.Fail(errors);
            });
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, Unit>> DeleteConnectionAsync(string uuid, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Suppression du profil WiFi : {Uuid}", uuid);

        var result = await RunNmcliAsync(new[] { "connection", "delete", uuid }, cancellationToken);

        return result.Match(
            Succ: _ =>
            {
                _logger.LogInformation("Suppression du profil WiFi réussie : {Uuid}", uuid);
                return Validation<Error, Unit>.Success(Unit.Default);
            },
            Fail: errors =>
            {
                _logger.LogWarning("Échec de suppression du profil WiFi : {Uuid}", uuid);
                return Validation<Error, Unit>.Fail(errors);
            });
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, WifiLink>> GetActiveLinkAsync(CancellationToken cancellationToken = default)
    {
        // Volontairement sans scan : cette lecture est rafraîchie en continu par la
        // page de supervision, un scan nmcli complet y serait bien trop coûteux.
        var statusResult = await RunNmcliAsync(
            new[] { "-t", "-f", "DEVICE,TYPE,STATE,CONNECTION", "device", "status" },
            cancellationToken);

        var active = statusResult.Match(
            Succ: WifiLinkReader.ParseActiveDevice,
            Fail: _ => null);

        if (active is null)
        {
            // nmcli absent ou aucun périphérique géré : les interfaces du runtime
            // fournissent au moins l'adresse IP.
            _logger.LogDebug("Aucun périphérique actif rapporté par nmcli, repli sur les interfaces système");
            return Validation<Error, WifiLink>.Success(WifiLinkReader.ReadFromRuntimeInterfaces());
        }

        var (device, type, connection) = active.Value;
        var isWifi = type.Contains("wifi", StringComparison.OrdinalIgnoreCase);

        var addressResult = await RunNmcliAsync(
            new[] { "-t", "-f", "IP4.ADDRESS", "device", "show", device },
            cancellationToken);

        var ipAddress = addressResult.Match(
            Succ: WifiLinkReader.ParseIpAddress,
            Fail: _ => null);

        var link = new WifiLink(
            IsConnected: true,
            InterfaceName: device,
            Ssid: isWifi && !string.IsNullOrWhiteSpace(connection) ? connection : null,
            SignalPercent: isWifi ? await ReadSignalPercentAsync(device, cancellationToken) : null,
            IpAddress: ipAddress);

        return Validation<Error, WifiLink>.Success(link);
    }

    /// <summary>
    /// Lit la qualité du lien sans fil depuis /proc/net/wireless.
    /// Retourne null lorsque la source est absente (plateforme non Linux, conteneur restreint).
    /// </summary>
    private async Task<int?> ReadSignalPercentAsync(string device, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_procNetWirelessPath))
                return null;

            var content = await File.ReadAllTextAsync(_procNetWirelessPath, cancellationToken);
            return WifiLinkReader.ParseSignalPercent(content, device);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Qualité du lien sans fil illisible pour {Device}", device);
            return null;
        }
    }

    /// <summary>
    /// Parse la sortie nmcli de la liste des réseaux WiFi.
    /// Format : IN-USE SSID MODE CHAN RATE SIGNAL BARS SECURITY
    /// </summary>
    internal static IReadOnlyList<WifiNetwork> ParseNetworksOutput(string output)
    {
        var networks = new List<WifiNetwork>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
            return networks.AsReadOnly();

        // Ignorer la ligne d'en-tête
        var headerLine = lines[0];

        // Déterminer les positions des colonnes à partir de l'en-tête
        var inUseStart = headerLine.IndexOf("IN-USE", StringComparison.OrdinalIgnoreCase);
        var ssidStart = headerLine.IndexOf("SSID", StringComparison.OrdinalIgnoreCase);
        var modeStart = headerLine.IndexOf("MODE", StringComparison.OrdinalIgnoreCase);
        var chanStart = headerLine.IndexOf("CHAN", StringComparison.OrdinalIgnoreCase);
        var rateStart = headerLine.IndexOf("RATE", StringComparison.OrdinalIgnoreCase);
        var signalStart = headerLine.IndexOf("SIGNAL", StringComparison.OrdinalIgnoreCase);
        var barsStart = headerLine.IndexOf("BARS", StringComparison.OrdinalIgnoreCase);
        var securityStart = headerLine.IndexOf("SECURITY", StringComparison.OrdinalIgnoreCase);

        if (ssidStart < 0 || signalStart < 0)
            return networks.AsReadOnly();

        foreach (var line in lines.Skip(1))
        {
            if (line.Length < ssidStart)
                continue;

            try
            {
                var inUseVal = inUseStart >= 0 && inUseStart < line.Length
                    ? ExtractColumn(line, inUseStart, ssidStart).Trim()
                    : "";
                var ssid = ssidStart >= 0 && ssidStart < line.Length
                    ? ExtractColumn(line, ssidStart, modeStart >= 0 ? modeStart : line.Length).Trim()
                    : "";
                var mode = modeStart >= 0 && modeStart < line.Length
                    ? ExtractColumn(line, modeStart, chanStart >= 0 ? chanStart : line.Length).Trim()
                    : "";
                var chan = chanStart >= 0 && chanStart < line.Length
                    ? ExtractColumn(line, chanStart, rateStart >= 0 ? rateStart : line.Length).Trim()
                    : "";
                var rate = rateStart >= 0 && rateStart < line.Length
                    ? ExtractColumn(line, rateStart, signalStart >= 0 ? signalStart : line.Length).Trim()
                    : "";
                var signalStr = signalStart >= 0 && signalStart < line.Length
                    ? ExtractColumn(line, signalStart, barsStart >= 0 ? barsStart : line.Length).Trim()
                    : "0";
                var barsStr = barsStart >= 0 && barsStart < line.Length
                    ? ExtractColumn(line, barsStart, securityStart >= 0 ? securityStart : line.Length).Trim()
                    : "";
                var security = securityStart >= 0 && securityStart < line.Length
                    ? line[securityStart..].Trim()
                    : "";

                if (string.IsNullOrWhiteSpace(ssid))
                    continue;

                var inUse = inUseVal == "*";
                var signal = int.TryParse(signalStr, out var s) ? s : 0;
                var bars = ComputeBars(signal);

                networks.Add(new WifiNetwork(
                    InUse: inUse,
                    Ssid: ssid,
                    Mode: mode,
                    Channel: chan,
                    Rate: rate,
                    Signal: signal,
                    Bars: bars,
                    Security: security,
                    HasSavedProfile: false,
                    ConnectionUuid: null));
            }
            catch
            {
                // Ligne malformée, on l'ignore
            }
        }

        // Dédoublonner par SSID (garder le meilleur signal)
        return networks
            .GroupBy(n => n.Ssid)
            .Select(g => g.OrderByDescending(n => n.InUse).ThenByDescending(n => n.Signal).First())
            .OrderByDescending(n => n.InUse)
            .ThenByDescending(n => n.Signal)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Parse la sortie nmcli de la liste des connexions sauvegardées.
    /// Filtre uniquement les connexions de type WiFi (802-11-wireless).
    /// </summary>
    internal static IReadOnlyList<WifiConnection> ParseConnectionsOutput(string output)
    {
        var connections = new List<WifiConnection>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
            return connections.AsReadOnly();

        var headerLine = lines[0];
        var nameStart = headerLine.IndexOf("NAME", StringComparison.OrdinalIgnoreCase);
        var uuidStart = headerLine.IndexOf("UUID", StringComparison.OrdinalIgnoreCase);
        var typeStart = headerLine.IndexOf("TYPE", StringComparison.OrdinalIgnoreCase);
        var deviceStart = headerLine.IndexOf("DEVICE", StringComparison.OrdinalIgnoreCase);

        if (nameStart < 0 || uuidStart < 0 || typeStart < 0)
            return connections.AsReadOnly();

        foreach (var line in lines.Skip(1))
        {
            if (line.Length < uuidStart)
                continue;

            try
            {
                var name = ExtractColumn(line, nameStart, uuidStart).Trim();
                var uuid = ExtractColumn(line, uuidStart, typeStart).Trim();
                var type = deviceStart >= 0
                    ? ExtractColumn(line, typeStart, deviceStart).Trim()
                    : line[typeStart..].Trim();
                var device = deviceStart >= 0 && deviceStart < line.Length
                    ? line[deviceStart..].Trim()
                    : "";

                if (string.IsNullOrWhiteSpace(uuid))
                    continue;

                // Filtrer uniquement les connexions WiFi
                if (!type.Contains("wireless", StringComparison.OrdinalIgnoreCase) &&
                    !type.Contains("wifi", StringComparison.OrdinalIgnoreCase))
                    continue;

                connections.Add(new WifiConnection(
                    Name: name,
                    Uuid: uuid,
                    Type: type,
                    Device: device));
            }
            catch
            {
                // Ligne malformée, on l'ignore
            }
        }

        return connections.AsReadOnly();
    }

    /// <summary>
    /// Exécute nmcli directement avec la liste d'arguments fournie.
    /// Les arguments sont passés sans interprétation shell, ce qui évite les injections de commandes.
    /// </summary>
    private async Task<Validation<Error, string>> RunNmcliAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "nmcli",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            var process = new Process { StartInfo = startInfo };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            {
                _logger.LogWarning("Commande nmcli échouée (exit {Code}): {Error}", process.ExitCode, error);
                return Error.Validation("WIFI_COMMAND_FAILED", $"Commande nmcli échouée : {error.Trim()}").ToFailure<string>();
            }

            return Validation<Error, string>.Success(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'exécution de nmcli");
            return Error.Validation("WIFI_COMMAND_EXCEPTION", $"Erreur d'exécution nmcli : {ex.Message}").ToFailure<string>();
        }
    }

    /// <summary>
    /// Extrait une colonne à une position donnée dans une ligne de sortie nmcli.
    /// </summary>
    private static string ExtractColumn(string line, int start, int end)
    {
        if (start >= line.Length)
            return string.Empty;
        var actualEnd = Math.Min(end, line.Length);
        return line[start..actualEnd];
    }

    /// <summary>
    /// Calcule le nombre de barres de signal (0-4) à partir du pourcentage de signal.
    /// </summary>
    internal static int ComputeBars(int signal)
    {
        return signal switch
        {
            >= 80 => 4,
            >= 60 => 3,
            >= 40 => 2,
            >= 20 => 1,
            _ => 0
        };
    }
}
