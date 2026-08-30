using LanguageExt;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Wifi;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Infrastructure.Network;

/// <summary>
/// Implémentation mock du service WiFi pour l'environnement de développement / Docker.
/// Simule 3-4 réseaux fictifs et les opérations de connexion en mémoire.
/// Activé via la configuration Wifi:UseMock = true.
/// </summary>
public class WifiMockService : IWifiService
{
    private readonly ILogger<WifiMockService> _logger;

    // UUID fixes pour les profils sauvegardés simulés
    private const string SavedUuid1 = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string SavedUuid2 = "bbbbbbbb-0000-0000-0000-000000000002";

    // Interface et adresse IP simulées du lien actif
    private const string MockInterfaceName = "wlan0";
    private const string MockIpAddress = "192.168.1.42";

    // État interne : SSID actuellement connecté
    private string? _connectedSsid;
    private string? _connectedUuid;

    // Réseaux fictifs fixes
    private static readonly IReadOnlyList<WifiNetwork> FakeNetworks = new List<WifiNetwork>
    {
        new WifiNetwork(
            InUse: false,
            Ssid: "HomeNetwork",
            Mode: "Infra",
            Channel: "6",
            Rate: "130 Mbit/s",
            Signal: 85,
            Bars: 4,
            Security: "WPA2",
            HasSavedProfile: true,
            ConnectionUuid: SavedUuid1),

        new WifiNetwork(
            InUse: false,
            Ssid: "Voisin-Box",
            Mode: "Infra",
            Channel: "11",
            Rate: "54 Mbit/s",
            Signal: 55,
            Bars: 3,
            Security: "WPA2",
            HasSavedProfile: false,
            ConnectionUuid: null),

        new WifiNetwork(
            InUse: false,
            Ssid: "F5ZVB-AP",
            Mode: "Infra",
            Channel: "1",
            Rate: "300 Mbit/s",
            Signal: 72,
            Bars: 3,
            Security: "WPA2",
            HasSavedProfile: true,
            ConnectionUuid: SavedUuid2),

        new WifiNetwork(
            InUse: false,
            Ssid: "OpenWifi",
            Mode: "Infra",
            Channel: "3",
            Rate: "54 Mbit/s",
            Signal: 30,
            Bars: 2,
            Security: "--",
            HasSavedProfile: false,
            ConnectionUuid: null)
    }.AsReadOnly();

    // Connexions sauvegardées fictives
    private static readonly IReadOnlyList<WifiConnection> FakeConnections = new List<WifiConnection>
    {
        new WifiConnection("HomeNetwork", SavedUuid1, "802-11-wireless", ""),
        new WifiConnection("F5ZVB-AP", SavedUuid2, "802-11-wireless", "")
    }.AsReadOnly();

    public WifiMockService(ILogger<WifiMockService> logger)
    {
        _logger = logger;
        // Par défaut, connecté à HomeNetwork
        _connectedSsid = "HomeNetwork";
        _connectedUuid = SavedUuid1;
    }

    /// <inheritdoc/>
    public Task<Validation<Error, IReadOnlyList<WifiNetwork>>> ScanNetworksAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK WiFi: Scan des réseaux simulés");

        // Simuler l'état de connexion actuel
        var networks = FakeNetworks
            .Select(n => n with { InUse = n.Ssid == _connectedSsid })
            .ToList()
            .AsReadOnly();

        return Task.FromResult(Validation<Error, IReadOnlyList<WifiNetwork>>.Success((IReadOnlyList<WifiNetwork>)networks));
    }

    /// <inheritdoc/>
    public Task<Validation<Error, WifiLink>> GetActiveLinkAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK WiFi: Lecture du lien actif simulé");

        if (_connectedSsid is null)
            return Task.FromResult(Validation<Error, WifiLink>.Success(
                new WifiLink(false, MockInterfaceName, null, null, null)));

        var network = FakeNetworks.FirstOrDefault(n =>
            string.Equals(n.Ssid, _connectedSsid, StringComparison.OrdinalIgnoreCase));

        var link = new WifiLink(
            IsConnected: true,
            InterfaceName: MockInterfaceName,
            Ssid: _connectedSsid,
            SignalPercent: network?.Signal,
            IpAddress: MockIpAddress);

        return Task.FromResult(Validation<Error, WifiLink>.Success(link));
    }

    /// <inheritdoc/>
    public Task<Validation<Error, IReadOnlyList<WifiConnection>>> GetSavedConnectionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK WiFi: Récupération des connexions sauvegardées simulées");
        return Task.FromResult(Validation<Error, IReadOnlyList<WifiConnection>>.Success(FakeConnections));
    }

    /// <inheritdoc/>
    public Task<Validation<Error, Unit>> ConnectAsync(string ssid, string password, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK WiFi: Connexion au réseau {Ssid} (mot de passe non loggé)", ssid);

        // Trouver le réseau dans la liste
        var network = FakeNetworks.FirstOrDefault(n =>
            string.Equals(n.Ssid, ssid, StringComparison.OrdinalIgnoreCase));

        if (network == null)
        {
            _logger.LogWarning("MOCK WiFi: Réseau {Ssid} non trouvé", ssid);
            return Task.FromResult(Error.Validation("WIFI_NOT_FOUND", $"Réseau '{ssid}' introuvable.").ToFailure<Unit>());
        }

        _connectedSsid = ssid;
        _connectedUuid = network.ConnectionUuid ?? $"mock-{Guid.NewGuid()}";
        _logger.LogInformation("MOCK WiFi: Connecté à {Ssid}", ssid);
        return Task.FromResult(Validation<Error, Unit>.Success(Unit.Default));
    }

    /// <inheritdoc/>
    public Task<Validation<Error, Unit>> ActivateConnectionAsync(string uuid, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK WiFi: Activation de la connexion {Uuid}", uuid);

        var connection = FakeConnections.FirstOrDefault(c =>
            string.Equals(c.Uuid, uuid, StringComparison.OrdinalIgnoreCase));

        if (connection == null)
        {
            _logger.LogWarning("MOCK WiFi: Connexion {Uuid} non trouvée", uuid);
            return Task.FromResult(Error.Validation("WIFI_CONNECTION_NOT_FOUND", $"Connexion '{uuid}' introuvable.").ToFailure<Unit>());
        }

        _connectedSsid = connection.Name;
        _connectedUuid = uuid;
        _logger.LogInformation("MOCK WiFi: Connexion activée : {Name}", connection.Name);
        return Task.FromResult(Validation<Error, Unit>.Success(Unit.Default));
    }

    /// <inheritdoc/>
    public Task<Validation<Error, Unit>> DeactivateConnectionAsync(string uuid, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK WiFi: Désactivation de la connexion {Uuid}", uuid);

        if (_connectedUuid != uuid)
        {
            _logger.LogWarning("MOCK WiFi: La connexion {Uuid} n'est pas active", uuid);
            return Task.FromResult(Error.Validation("WIFI_NOT_ACTIVE", $"La connexion '{uuid}' n'est pas active.").ToFailure<Unit>());
        }

        _connectedSsid = null;
        _connectedUuid = null;
        _logger.LogInformation("MOCK WiFi: Connexion désactivée");
        return Task.FromResult(Validation<Error, Unit>.Success(Unit.Default));
    }

    /// <inheritdoc/>
    public Task<Validation<Error, Unit>> DeleteConnectionAsync(string uuid, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK WiFi: Suppression du profil {Uuid}", uuid);

        // Dans le mock, on simule juste la déconnexion si c'était le profil actif
        if (_connectedUuid == uuid)
        {
            _connectedSsid = null;
            _connectedUuid = null;
        }

        _logger.LogInformation("MOCK WiFi: Profil supprimé (simulé) : {Uuid}", uuid);
        return Task.FromResult(Validation<Error, Unit>.Success(Unit.Default));
    }
}
