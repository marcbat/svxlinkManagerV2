using MediatR;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Application.Features.Statistics.GetStatistics;

/// <summary>
/// Query d'agrégation de l'historique d'activité sur une période.
/// </summary>
/// <param name="Period">Fenêtre d'observation demandée.</param>
public record GetStatisticsQuery(StatisticsPeriod Period = StatisticsPeriod.Last7Days)
    : IRequest<StatisticsDto>;

/// <summary>
/// Handler de <see cref="GetStatisticsQuery"/>.
///
/// Le gros du regroupement est délégué à SQLite par le repository ; ce handler ne fait que
/// recouper les résultats, calculer les parts et rattraper les intervalles encore ouverts
/// (session en cours, liaison réflecteur en cours) que l'écriture ne connaît pas encore.
/// </summary>
public class GetStatisticsQueryHandler : IRequestHandler<GetStatisticsQuery, StatisticsDto>
{
    /// <summary>Nom donné aux périodes passées hors salon.</summary>
    internal const string StandaloneName = ActivityLabels.StandaloneSalonName;

    private readonly IActivityRepository _repository;
    private readonly ISalonRepository _salonRepository;
    private readonly IActivityRecorder _recorder;
    private readonly StatisticsOptions _options;

    public GetStatisticsQueryHandler(
        IActivityRepository repository,
        ISalonRepository salonRepository,
        IActivityRecorder recorder,
        IOptions<StatisticsOptions> options)
    {
        _repository = repository;
        _salonRepository = salonRepository;
        _recorder = recorder;
        _options = options.Value;
    }

    public async Task<StatisticsDto> Handle(GetStatisticsQuery query, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        var from = query.Period.ToStartUtc(now);

        var sessions = await _repository.GetSessionsAsync(from, cancellationToken);
        var eventSummaries = (await _repository.GetEventSummariesAsync(from, cancellationToken))
            .ToDictionary(s => s.Type);

        var salons = await _salonRepository.GetAllAsync(cancellationToken);
        var activeSalons = salons.Where(s => !s.IsDeleted).ToList();

        var usage = BuildSalonUsage(sessions, from, now);
        var trackedTime = Sum(usage.Select(u => u.TotalTime));

        var traffic = await BuildTrafficAsync(eventSummaries, from, cancellationToken);
        var dtmf = await BuildDtmfAsync(activeSalons, from, cancellationToken);
        var local = await BuildLocalActivityAsync(eventSummaries, usage, cancellationToken);
        var reliability = BuildReliability(eventSummaries, usage, from, now);

        var startedInPeriod = sessions.Where(s => s.StartedAt >= from).ToList();
        var historyStart = (await _repository.GetFirstActivityAtAsync(cancellationToken))?.ToLocalTime();

        return new StatisticsDto(
            CollectedAt: now,
            Period: query.Period,
            // Sur « tout l'historique », la fenêtre commence au plus ancien enregistrement conservé,
            // pas à DateTimeOffset.MinValue qui n'apprendrait rien à l'opérateur.
            PeriodStart: query.Period == StatisticsPeriod.All ? historyStart : from.ToLocalTime(),
            HistoryStart: historyStart,
            RetentionDays: _options.RetentionDays,
            Summary: new StatisticsSummaryDto(
                TrackedTime: trackedTime,
                DistinctSalonCount: usage.Count(u => u.SalonId.HasValue),
                TalkerCount: traffic.Count,
                TalkerTime: traffic.TotalTime,
                DtmfCount: dtmf.TotalCount,
                LinkAvailabilityPercent: reliability.AvailabilityPercent),
            SalonUsage: usage,
            UnusedSalonNames: activeSalons
                .Where(s => usage.All(u => u.SalonId != s.Id))
                .Select(s => s.Name)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
                .AsReadOnly(),
            ActivationOrigins: BuildOrigins(startedInPeriod),
            Traffic: traffic,
            HourlyActivity: await _repository.GetHourlyActivityAsync(from, cancellationToken),
            LocalActivity: local,
            Dtmf: dtmf,
            Reliability: reliability,
            Timeline: await BuildTimelineAsync(from, cancellationToken));
    }

    /// <summary>
    /// Regroupe les sessions par salon en bornant chacune à la période : une session commencée
    /// avant la fenêtre ne compte que par son recouvrement, et une session encore ouverte court
    /// jusqu'à l'instant de la collecte.
    ///
    /// Le nombre d'activations, lui, ne compte que les sessions **commencées** dans la fenêtre :
    /// une session héritée de la veille apporte du temps d'antenne, pas une activation de plus.
    /// </summary>
    internal static IReadOnlyList<SalonUsageDto> BuildSalonUsage(
        IReadOnlyList<SalonSession> sessions,
        DateTimeOffset from,
        DateTimeOffset now)
    {
        var nowUtc = now.ToUniversalTime();

        var groups = sessions
            // La clé exclut le nom : un salon renommé en cours de route ne doit pas apparaître deux fois.
            .GroupBy(s => (s.SalonId, s.Kind))
            .Select(group =>
            {
                var ordered = group.OrderByDescending(s => s.StartedAt).ToList();
                var latest = ordered[0];

                var total = TimeSpan.Zero;
                foreach (var session in ordered)
                {
                    var start = session.StartedAt < from ? from : session.StartedAt;
                    var end = session.EndedAt ?? nowUtc;
                    if (end > start)
                        total += end - start;
                }

                var activations = ordered.Count(s => s.StartedAt >= from);

                return new SalonUsageDto(
                    SalonId: latest.SalonId,
                    Name: latest.SalonId is null ? StandaloneName : latest.SalonName,
                    Kind: latest.Kind,
                    TotalTime: total,
                    SharePercent: 0,
                    SessionCount: activations,
                    AverageSessionTime: activations > 0
                        ? TimeSpan.FromSeconds(total.TotalSeconds / activations)
                        : total,
                    LastStartedAt: latest.StartedAt.ToLocalTime(),
                    IsOngoing: ordered.Any(s => s.IsOpen));
            })
            .Where(u => u.TotalTime > TimeSpan.Zero || u.IsOngoing)
            .OrderByDescending(u => u.TotalTime)
            .ToList();

        var overall = groups.Aggregate(TimeSpan.Zero, (acc, u) => acc + u.TotalTime);

        return groups
            .Select(u => u with
            {
                SharePercent = overall > TimeSpan.Zero
                    ? u.TotalTime.TotalSeconds / overall.TotalSeconds * 100
                    : 0
            })
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyList<ActivationOriginDto> BuildOrigins(IReadOnlyList<SalonSession> startedInPeriod)
    {
        if (startedInPeriod.Count == 0)
            return Array.Empty<ActivationOriginDto>();

        return startedInPeriod
            .GroupBy(s => s.Origin)
            .Select(g => new ActivationOriginDto(
                g.Key,
                g.Count(),
                (double)g.Count() / startedInPeriod.Count * 100))
            .OrderByDescending(o => o.Count)
            .ToList()
            .AsReadOnly();
    }

    private async Task<TrafficDto> BuildTrafficAsync(
        IReadOnlyDictionary<ActivityEventType, ActivityEventSummary> summaries,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        var talker = Get(summaries, ActivityEventType.TalkerHeard);

        var perSalon = (await _repository.GetSalonEventSummariesAsync(from, ActivityEventType.TalkerHeard, cancellationToken))
            .Select(s => new SalonTrafficDto(
                s.SalonName ?? StandaloneName,
                s.Count,
                TimeSpan.FromSeconds(s.TotalSeconds)))
            .OrderByDescending(s => s.TotalTime)
            .ToList()
            .AsReadOnly();

        var top = (await _repository.GetTopCallsignsAsync(from, _options.TopCallsignCount, cancellationToken))
            .Select(c => new CallsignTrafficDto(
                c.Callsign,
                c.Count,
                TimeSpan.FromSeconds(c.TotalSeconds),
                c.LastHeardAt.ToLocalTime()))
            .ToList()
            .AsReadOnly();

        return new TrafficDto(
            Count: talker.Count,
            TotalTime: TimeSpan.FromSeconds(talker.TotalSeconds),
            AverageTime: Average(talker),
            LongestTime: TimeSpan.FromSeconds(talker.MaxSeconds),
            DistinctCallsignCount: await _repository.GetDistinctCallsignCountAsync(from, cancellationToken),
            TopCallsigns: top,
            PerSalon: perSalon);
    }

    private async Task<LocalActivityDto> BuildLocalActivityAsync(
        IReadOnlyDictionary<ActivityEventType, ActivityEventSummary> summaries,
        IReadOnlyList<SalonUsageDto> usage,
        CancellationToken cancellationToken)
    {
        var localTx = Get(summaries, ActivityEventType.LocalTransmission);
        var parrot = usage.Where(u => u.Kind == SalonKind.Parrot).ToList();

        return new LocalActivityDto(
            TransmissionCount: localTx.Count,
            TotalTime: TimeSpan.FromSeconds(localTx.TotalSeconds),
            AverageTime: Average(localTx),
            LongestTime: TimeSpan.FromSeconds(localTx.MaxSeconds),
            DistortionCount: Get(summaries, ActivityEventType.RxDistortion).Count,
            ParrotTime: Sum(parrot.Select(p => p.TotalTime)),
            ParrotSessionCount: parrot.Sum(p => p.SessionCount),
            IsSquelchTrackingObserved:
                await _repository.HasAnyEventAsync(ActivityEventType.LocalTransmission, cancellationToken));
    }

    private async Task<DtmfStatisticsDto> BuildDtmfAsync(
        IReadOnlyList<SalonAggregate> salons,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        // Un salon peut n'avoir aucun code DTMF, et deux salons ne devraient pas partager le même :
        // le regroupement protège malgré tout d'une base incohérente, qui ferait lever ToDictionary.
        var salonNamesByCode = salons
            .Where(s => s.DtmfCode.HasValue)
            .GroupBy(s => s.DtmfCode!.Value)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var usages = (await _repository.GetDtmfSummariesAsync(from, cancellationToken))
            .Select(summary =>
            {
                var (category, label) = DtmfCommandClassifier.Classify(summary.Code, salonNamesByCode);
                return new DtmfCodeUsageDto(
                    summary.Code,
                    label,
                    category,
                    summary.Count,
                    summary.LastUsedAt.ToLocalTime());
            })
            .ToList();

        return new DtmfStatisticsDto(
            TotalCount: usages.Sum(u => u.Count),
            CountsByCategory: usages
                .GroupBy(u => u.Category)
                .Select(g => (Category: g.Key, Count: g.Sum(u => u.Count)))
                .OrderByDescending(g => g.Count)
                .ToList()
                .AsReadOnly(),
            TopCodes: usages
                .OrderByDescending(u => u.Count)
                .ThenBy(u => u.Code, StringComparer.Ordinal)
                .Take(_options.TopDtmfCodeCount)
                .ToList()
                .AsReadOnly(),
            UnmatchedCodes: usages
                .Where(u => u.Category == DtmfCommandCategory.Unknown)
                .OrderByDescending(u => u.Count)
                .Take(_options.TopDtmfCodeCount)
                .ToList()
                .AsReadOnly(),
            LastUsedAt: usages.Count == 0 ? null : usages.Max(u => u.LastUsedAt));
    }

    /// <summary>
    /// Rapporte le temps de liaison effective au temps passé sur un salon réflecteur.
    /// La liaison en cours, pas encore écrite, est rattrapée depuis le recorder — sans quoi
    /// un nœud lié sans interruption depuis des jours afficherait une disponibilité nulle.
    /// </summary>
    private ReliabilityDto BuildReliability(
        IReadOnlyDictionary<ActivityEventType, ActivityEventSummary> summaries,
        IReadOnlyList<SalonUsageDto> usage,
        DateTimeOffset from,
        DateTimeOffset now)
    {
        var linkUp = Get(summaries, ActivityEventType.ReflectorLinkUp);
        var linked = TimeSpan.FromSeconds(linkUp.TotalSeconds);
        var longestStretch = TimeSpan.FromSeconds(linkUp.MaxSeconds);

        if (_recorder.PendingLinkUpSince is { } since)
        {
            var start = since.ToUniversalTime() < from ? from : since.ToUniversalTime();
            var ongoing = now.ToUniversalTime() - start;
            if (ongoing > TimeSpan.Zero)
            {
                linked += ongoing;
                if (ongoing > longestStretch)
                    longestStretch = ongoing;
            }
        }

        var reflectorTime = Sum(usage.Where(u => u.Kind == SalonKind.Reflector).Select(u => u.TotalTime));

        // Les deux mesures ne viennent pas de la même source (sessions d'un côté, logs de l'autre) :
        // un léger dépassement est possible et serait absurde à afficher au-delà de 100 %.
        double? availability = reflectorTime > TimeSpan.Zero
            ? Math.Min(100, linked.TotalSeconds / reflectorTime.TotalSeconds * 100)
            : null;

        return new ReliabilityDto(
            LinkedTime: linked,
            ReflectorSessionTime: reflectorTime,
            AvailabilityPercent: availability,
            DisconnectionCount: Get(summaries, ActivityEventType.ReflectorLinkLost).Count,
            FailureCount: Get(summaries, ActivityEventType.ReflectorLinkFailed).Count,
            LongestOutage: TimeSpan.FromSeconds(Get(summaries, ActivityEventType.ReflectorOutage).MaxSeconds),
            LongestLinkedStretch: longestStretch,
            ApplicationStartCount: Get(summaries, ActivityEventType.ApplicationStarted).Count);
    }

    private async Task<IReadOnlyList<TimelineEntryDto>> BuildTimelineAsync(
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        var events = await _repository.GetRecentEventsAsync(from, _options.TimelineEntryCount, cancellationToken);

        return events
            .Select(e => new TimelineEntryDto(
                OccurredAt: e.OccurredAt.ToLocalTime(),
                Type: e.Type,
                Label: Describe(e),
                SalonName: e.SalonName,
                Duration: e.DurationSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>Description lisible d'un événement, pour la chronologie et l'export CSV.</summary>
    /// <param name="activityEvent">Événement à décrire.</param>
    public static string Describe(ActivityEvent activityEvent) => activityEvent.Type switch
    {
        ActivityEventType.TalkerHeard => $"Passage de {activityEvent.Callsign ?? "un nœud inconnu"}",
        ActivityEventType.LocalTransmission => "Réception locale (squelch ouvert)",
        ActivityEventType.DtmfCommand => $"Code DTMF {activityEvent.Detail}",
        ActivityEventType.ReflectorLinkUp => "Fin d'une période de liaison au réflecteur",
        ActivityEventType.ReflectorLinkLost => $"Liaison réflecteur perdue{Suffix(activityEvent.Detail)}",
        ActivityEventType.ReflectorLinkFailed => $"Liaison réflecteur impossible{Suffix(activityEvent.Detail)}",
        ActivityEventType.ReflectorOutage => "Liaison réflecteur rétablie",
        ActivityEventType.RxDistortion => "Écrêtage de l'audio en réception",
        ActivityEventType.ApplicationStarted => "Démarrage de l'application",
        ActivityEventType.ApplicationStopped => "Arrêt de l'application",
        _ => activityEvent.Type.ToString()
    };

    private static string Suffix(string? detail) =>
        string.IsNullOrWhiteSpace(detail) ? string.Empty : $" — {detail}";

    private static ActivityEventSummary Get(
        IReadOnlyDictionary<ActivityEventType, ActivityEventSummary> summaries,
        ActivityEventType type)
        => summaries.TryGetValue(type, out var summary)
            ? summary
            : new ActivityEventSummary(type, 0, 0, 0);

    private static TimeSpan Average(ActivityEventSummary summary)
        => summary.Count == 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds((double)summary.TotalSeconds / summary.Count);

    private static TimeSpan Sum(IEnumerable<TimeSpan> values)
        => values.Aggregate(TimeSpan.Zero, (acc, value) => acc + value);
}
