using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Statistics;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Infrastructure.Statistics;

/// <summary>
/// Enregistreur de l'historique d'activité. Singleton : il mémorise le salon courant et les
/// intervalles encore ouverts, que la base ne connaît pas tant qu'ils ne sont pas terminés.
///
/// Chaque écriture ouvre son propre scope de <c>DbContext</c> : les appelants sont des singletons
/// (trackers, services hébergés) qui ne peuvent pas capturer un contexte scoped, et deux écritures
/// concurrentes ne doivent pas se partager le même <c>DbContext</c>, qui n'est pas thread-safe.
///
/// Aucune méthode ne lève : une statistique perdue est un moindre mal comparé à une activation
/// de salon qui échoue ou à une commande DTMF avalée.
/// </summary>
public class ActivityRecorder : IActivityRecorder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityRecorder> _logger;
    private readonly object _lock = new();

    private Guid? _currentSalonId;
    private string? _currentSalonName;
    private ReflectorLinkStatus _linkStatus = ReflectorLinkStatus.Inactive;
    private DateTimeOffset? _linkUpSince;
    private DateTimeOffset? _linkLostAt;

    public ActivityRecorder(
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityRecorder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public DateTimeOffset? PendingLinkUpSince
    {
        get { lock (_lock) return _linkUpSince; }
    }

    public async Task RecordSessionStartAsync(
        Guid? salonId,
        string salonName,
        SalonKind kind,
        SalonActivationOrigin origin,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _currentSalonId = salonId;
            _currentSalonName = salonId is null ? ActivityLabels.StandaloneSalonName : salonName;
        }

        var session = SalonSession.Start(salonId, salonName, kind, origin, DateTimeOffset.UtcNow);

        await ExecuteAsync(
            repository => repository.StartSessionAsync(session, cancellationToken),
            $"ouverture de la session {salonName}");
    }

    public async Task CloseCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _currentSalonId = null;
            _currentSalonName = null;
        }

        await ExecuteAsync(
            repository => repository.CloseOpenSessionsAsync(DateTimeOffset.UtcNow, false, cancellationToken),
            "clôture de la session courante");
    }

    public async Task RecordEventAsync(
        ActivityEventType type,
        string? callsign = null,
        TimeSpan? duration = null,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        Guid? salonId;
        string? salonName;
        lock (_lock)
        {
            salonId = _currentSalonId;
            salonName = _currentSalonName;
        }

        var activityEvent = ActivityEvent.Create(
            type,
            DateTimeOffset.UtcNow,
            salonId,
            salonName,
            callsign,
            duration,
            detail);

        await ExecuteAsync(
            repository => repository.AddEventAsync(activityEvent, cancellationToken),
            $"enregistrement de l'événement {type}");
    }

    /// <summary>
    /// Traduit une transition d'état en événements de durée.
    ///
    /// Deux intervalles sont tenus ici : celui de la liaison établie, écrit quand elle se
    /// termine, et celui de l'interruption, écrit quand la liaison revient. Aucun des deux
    /// n'est connaissable au moment où il commence.
    /// </summary>
    public async Task RecordLinkStateAsync(ReflectorLinkState state, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        TimeSpan? linkedDuration = null;
        TimeSpan? outageDuration = null;
        ActivityEventType? lossEvent = null;

        lock (_lock)
        {
            if (state.Status == _linkStatus)
                return;

            var wasConnected = _linkStatus == ReflectorLinkStatus.Connected;
            _linkStatus = state.Status;

            if (wasConnected && _linkUpSince is { } since)
            {
                linkedDuration = now - since;
                _linkUpSince = null;
            }

            switch (state.Status)
            {
                case ReflectorLinkStatus.Connected:
                    _linkUpSince = now;
                    if (_linkLostAt is { } lostAt)
                    {
                        outageDuration = now - lostAt;
                        _linkLostAt = null;
                    }
                    break;

                case ReflectorLinkStatus.Disconnected:
                    lossEvent = ActivityEventType.ReflectorLinkLost;
                    _linkLostAt = now;
                    break;

                case ReflectorLinkStatus.Failed:
                    lossEvent = ActivityEventType.ReflectorLinkFailed;
                    _linkLostAt = now;
                    break;

                case ReflectorLinkStatus.NotApplicable:
                case ReflectorLinkStatus.Inactive:
                    // Aucune liaison n'est plus attendue : mesurer une interruption n'aurait
                    // plus de sens, elle serait comptée jusqu'au prochain salon réflecteur.
                    _linkLostAt = null;
                    break;
            }
        }

        if (linkedDuration is { } linked)
            await RecordEventAsync(ActivityEventType.ReflectorLinkUp, duration: linked, cancellationToken: cancellationToken);

        if (outageDuration is { } outage)
            await RecordEventAsync(ActivityEventType.ReflectorOutage, duration: outage, cancellationToken: cancellationToken);

        if (lossEvent is { } loss)
            await RecordEventAsync(loss, detail: DescribeReason(state.Reason), cancellationToken: cancellationToken);
    }

    private async Task ExecuteAsync(
        Func<IActivityRepository, Task<Validation<Error, Unit>>> action,
        string description)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

            var result = await action(repository);

            result.Match(
                Succ: _ => Unit.Default,
                Fail: errors =>
                {
                    _logger.LogWarning(
                        "Historique d'activité — échec de l'{Description} : {Errors}",
                        description,
                        string.Join(", ", errors.Select(e => e.Message)));
                    return Unit.Default;
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Historique d'activité — échec de l'{Description}", description);
        }
    }

    private static string? DescribeReason(ReflectorLinkFailureReason reason) => reason switch
    {
        ReflectorLinkFailureReason.None => null,
        ReflectorLinkFailureReason.AuthenticationRejected => "authentification refusée",
        ReflectorLinkFailureReason.HostUnreachable => "hôte injoignable",
        ReflectorLinkFailureReason.CertificateRejected => "certificat rejeté",
        ReflectorLinkFailureReason.ProtocolError => "erreur de protocole",
        ReflectorLinkFailureReason.RemoteDisconnected => "fermeture par le réflecteur",
        ReflectorLinkFailureReason.HeartbeatTimeout => "plus de battement de cœur",
        ReflectorLinkFailureReason.ConfigurationInvalid => "configuration incomplète",
        _ => "cause inconnue"
    };
}
