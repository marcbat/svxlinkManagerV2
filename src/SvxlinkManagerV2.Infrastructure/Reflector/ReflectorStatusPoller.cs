using System.Globalization;
using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Features.Reflectors;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Infrastructure.Reflector;

/// <summary>
/// Interroge périodiquement le serveur HTTP de statut du réflecteur local et publie le
/// dernier instantané. Singleton, également service hébergé.
/// </summary>
/// <remarks>
/// <para>
/// Le port vient du fichier de configuration réellement utilisé par le démon, et non de la
/// base : c'est lui qui décide, et il est relu à chaque cycle pour qu'une modification
/// prenne effet sans redémarrer l'application.
/// </para>
/// <para>
/// L'hôte interrogé est celui de <see cref="LocalReflectorOptions"/> : la boucle locale en
/// production, où le démon tourne sur la machine elle-même, et le nom du service dans la
/// stack Docker, où il vit dans un autre conteneur. SVXLink, lui, lie ce port sur
/// <b>toutes</b> les interfaces (<c>Async::TcpServer</c> sans adresse — non configurable) :
/// sur une machine exposée, il doit être fermé au pare-feu. La manpage est explicite sur le
/// sujet, ce serveur n'étant ni audité ni prévu pour encaisser la charge.
/// </para>
/// </remarks>
public class ReflectorStatusPoller : IReflectorStatusService, IHostedService, IDisposable
{
    /// <summary>
    /// Cadence d'interrogation. Assez lente pour rester négligeable devant le trafic audio,
    /// assez rapide pour qu'un talker apparaisse pendant qu'il parle.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>Le serveur est local : au-delà, il ne répondra pas davantage.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

    private readonly ILogger<ReflectorStatusPoller> _logger;
    private readonly IReflectorDaemonService _daemonService;
    private readonly HttpClient _httpClient;
    private readonly ReflectorConfigurationFile _configuration;
    private readonly string _host;
    private readonly object _lock = new();

    private ReflectorStatusSnapshot _current = ReflectorStatusSnapshot.Unknown;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _disposed;

    public event Action<ReflectorStatusSnapshot>? OnStatusChanged;

    public ReflectorStatusSnapshot Current
    {
        get { lock (_lock) return _current; }
    }

    public ReflectorStatusPoller(
        ILogger<ReflectorStatusPoller> logger,
        IReflectorDaemonService daemonService,
        IOptions<LocalReflectorOptions> localReflector,
        HttpClient? httpClient = null,
        string? configPath = null)
    {
        _logger = logger;
        _daemonService = daemonService;
        _host = localReflector.Value.Host;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = RequestTimeout;
        _configuration = new ReflectorConfigurationFile(configPath);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _loop = PollAsync(_cts.Token);

        _logger.LogInformation("ReflectorStatusPoller démarré (cadence {Interval})", PollInterval);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken); }
            catch (Exception) { /* annulation ou arrêt forcé : rien à sauver */ }
        }

        _logger.LogInformation("ReflectorStatusPoller arrêté");
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                Publish(await ReadStatusAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Une boucle de supervision ne doit jamais emporter l'hôte avec elle.
                _logger.LogError(ex, "Erreur inattendue lors de l'interrogation du statut du réflecteur");
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    /// <summary>
    /// Produit l'instantané courant. Chaque indisponibilité est nommée : c'est ce qui permet
    /// à la page de dire pourquoi elle ne montre rien, au lieu d'afficher une liste vide.
    /// </summary>
    internal async Task<ReflectorStatusSnapshot> ReadStatusAsync(CancellationToken cancellationToken)
    {
        // L'état du processus n'est connaissable que s'il tourne sur cette machine.
        // Dans la stack Docker le réflecteur vit dans un autre conteneur : y renoncer
        // laisse simplement l'absence de réponse HTTP faire foi.
        if (IsLocalHost)
        {
            var running = await _daemonService.IsRunningAsync(cancellationToken);
            if (running.Match(Succ: r => !r, Fail: _ => true))
                return ReflectorStatusSnapshot.Unavailable(
                    ReflectorStatusAvailability.DaemonStopped,
                    "Le démon svxreflector ne tourne pas.");
        }

        var port = ReadHttpPort();
        if (port is null)
            return ReflectorStatusSnapshot.Unavailable(
                ReflectorStatusAvailability.PortNotConfigured,
                $"La configuration du réflecteur ne déclare pas {ReflectorConfigurationFile.HttpPortKey} dans [GLOBAL].");

        string body;
        try
        {
            var url = "http://" + _host + ":" + port.Value.ToString(CultureInfo.InvariantCulture) + "/status";
            body = await _httpClient.GetStringAsync(url, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ReflectorStatusSnapshot.Unavailable(
                ReflectorStatusAvailability.Unreachable,
                $"Le serveur de statut n'a pas répondu sur le port {port} en moins de {RequestTimeout.TotalSeconds:F0} s.");
        }
        catch (HttpRequestException ex)
        {
            return ReflectorStatusSnapshot.Unavailable(
                ReflectorStatusAvailability.Unreachable,
                $"Le serveur de statut est injoignable sur le port {port} : {ex.Message}");
        }

        var nodes = ReflectorStatusParser.Parse(body);
        if (nodes is null)
            return ReflectorStatusSnapshot.Unavailable(
                ReflectorStatusAvailability.Invalid,
                "La réponse du serveur de statut n'est pas exploitable.");

        return new ReflectorStatusSnapshot(
            ReflectorStatusAvailability.Available, nodes, DateTime.UtcNow);
    }

    /// <summary>
    /// Le réflecteur tourne-t-il sur cette machine ? Seul cas où l'état de son processus
    /// est observable.
    /// </summary>
    private bool IsLocalHost =>
        IPAddress.TryParse(_host, out var address)
            ? IPAddress.IsLoopback(address)
            : _host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Port déclaré par <c>HTTP_SRV_PORT</c>, ou <c>null</c> si la clé est absente,
    /// vide ou non numérique — trois façons de dire que l'API n'est pas activée.
    /// </summary>
    private int? ReadHttpPort()
    {
        try
        {
            return _configuration.ReadPort(ReflectorConfigurationFile.HttpPortKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lecture de {Key} impossible", ReflectorConfigurationFile.HttpPortKey);
            return null;
        }
    }

    /// <summary>
    /// Mémorise l'instantané et ne notifie que si son contenu a changé.
    /// </summary>
    /// <remarks>
    /// L'instantané est toujours remplacé, pour que l'heure de dernière lecture affichée
    /// reste juste ; l'événement, lui, n'est émis que sur un changement réel, sinon chaque
    /// circuit Blazor se redessinerait toutes les cinq secondes pour rien.
    /// </remarks>
    private void Publish(ReflectorStatusSnapshot next)
    {
        bool changed;
        lock (_lock)
        {
            changed = !SameContent(_current, next);
            _current = next;
        }

        if (!changed)
            return;

        if (next.IsAvailable)
            _logger.LogDebug("Statut du réflecteur : {Count} nœud(s)", next.Nodes.Count);
        else
            _logger.LogInformation("Statut du réflecteur indisponible : {Detail}", next.Detail);

        OnStatusChanged?.Invoke(next);
    }

    /// <summary>
    /// Compare deux instantanés sans tenir compte de leur horodatage.
    /// </summary>
    /// <remarks>
    /// La comparaison des nœuds est faite champ à champ : l'égalité générée par le record
    /// compare <c>MonitoredTalkGroups</c> par référence, et deux lectures successives
    /// produisent toujours des listes distinctes — tout aurait donc paru changer à chaque
    /// cycle.
    /// </remarks>
    private static bool SameContent(ReflectorStatusSnapshot a, ReflectorStatusSnapshot b) =>
        a.Availability == b.Availability
        && a.Detail == b.Detail
        && a.Nodes.Count == b.Nodes.Count
        && a.Nodes.Zip(b.Nodes, SameNode).All(same => same);

    private static bool SameNode(ReflectorNodeStatus a, ReflectorNodeStatus b) =>
        a.Callsign == b.Callsign
        && a.TalkGroup == b.TalkGroup
        && a.IsTalker == b.IsTalker
        && a.RestrictedTalkGroup == b.RestrictedTalkGroup
        && a.Software == b.Software
        && a.SoftwareVersion == b.SoftwareVersion
        && a.ProjectVersion == b.ProjectVersion
        && a.MachineArchitecture == b.MachineArchitecture
        && a.ProtocolVersion == b.ProtocolVersion
        && a.MonitoredTalkGroups.SequenceEqual(b.MonitoredTalkGroups);

    public void Dispose()
    {
        if (_disposed)
            return;

        _cts?.Cancel();
        _cts?.Dispose();
        _httpClient.Dispose();
        _disposed = true;
    }
}
