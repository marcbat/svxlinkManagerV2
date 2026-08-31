using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Application.Features.Statistics;

/// <summary>
/// Compteurs de tête de page.
/// </summary>
/// <param name="TrackedTime">Temps d'antenne suivi, toutes natures de session confondues.</param>
/// <param name="DistinctSalonCount">Nombre de salons distincts occupés, mode autonome exclu.</param>
/// <param name="TalkerCount">Nombre de passages distants entendus.</param>
/// <param name="TalkerTime">Durée cumulée de ces passages.</param>
/// <param name="DtmfCount">Nombre de commandes DTMF reçues.</param>
/// <param name="LinkAvailabilityPercent">Part du temps réflecteur effectivement liée, <c>null</c> si aucun salon réflecteur n'a tourné.</param>
public record StatisticsSummaryDto(
    TimeSpan TrackedTime,
    int DistinctSalonCount,
    int TalkerCount,
    TimeSpan TalkerTime,
    int DtmfCount,
    double? LinkAvailabilityPercent);

/// <summary>
/// Temps passé sur un salon, ou en mode autonome.
/// </summary>
/// <param name="SalonId">Salon concerné, <c>null</c> en mode autonome.</param>
/// <param name="Name">Nom affiché.</param>
/// <param name="Kind">Nature de la session.</param>
/// <param name="TotalTime">Temps cumulé sur la période.</param>
/// <param name="SharePercent">Part du temps d'antenne suivi.</param>
/// <param name="SessionCount">Nombre d'activations.</param>
/// <param name="AverageSessionTime">Durée moyenne d'une activation.</param>
/// <param name="LastStartedAt">Dernière activation, en heure locale.</param>
/// <param name="IsOngoing">Session encore en cours à l'instant de la collecte.</param>
public record SalonUsageDto(
    Guid? SalonId,
    string Name,
    SalonKind Kind,
    TimeSpan TotalTime,
    double SharePercent,
    int SessionCount,
    TimeSpan AverageSessionTime,
    DateTimeOffset? LastStartedAt,
    bool IsOngoing);

/// <summary>
/// Nombre d'activations imputables à une origine.
/// </summary>
/// <param name="Origin">Origine de l'activation.</param>
/// <param name="Count">Nombre d'activations.</param>
/// <param name="SharePercent">Part sur l'ensemble des activations.</param>
public record ActivationOriginDto(SalonActivationOrigin Origin, int Count, double SharePercent);

/// <summary>
/// Trafic attribué à un indicatif.
/// </summary>
/// <param name="Callsign">Indicatif du nœud distant.</param>
/// <param name="Count">Nombre de passages.</param>
/// <param name="TotalTime">Durée cumulée de parole.</param>
/// <param name="LastHeardAt">Dernier passage.</param>
public record CallsignTrafficDto(string Callsign, int Count, TimeSpan TotalTime, DateTimeOffset LastHeardAt);

/// <summary>
/// Trafic entendu sur un salon.
/// </summary>
/// <param name="SalonName">Nom du salon.</param>
/// <param name="Count">Nombre de passages.</param>
/// <param name="TotalTime">Durée cumulée.</param>
public record SalonTrafficDto(string SalonName, int Count, TimeSpan TotalTime);

/// <summary>
/// Trafic distant entendu pendant que le nœud était connecté.
/// </summary>
/// <param name="Count">Nombre de passages.</param>
/// <param name="TotalTime">Durée cumulée.</param>
/// <param name="AverageTime">Durée moyenne d'un passage.</param>
/// <param name="LongestTime">Passage le plus long.</param>
/// <param name="DistinctCallsignCount">Nombre d'indicatifs distincts entendus.</param>
/// <param name="TopCallsigns">Palmarès des indicatifs.</param>
/// <param name="PerSalon">Ventilation par salon.</param>
public record TrafficDto(
    int Count,
    TimeSpan TotalTime,
    TimeSpan AverageTime,
    TimeSpan LongestTime,
    int DistinctCallsignCount,
    IReadOnlyList<CallsignTrafficDto> TopCallsigns,
    IReadOnlyList<SalonTrafficDto> PerSalon);

/// <summary>
/// Activité radio locale : ce que le récepteur du nœud a entendu en direct.
/// </summary>
/// <param name="TransmissionCount">Nombre d'ouvertures du squelch.</param>
/// <param name="TotalTime">Durée cumulée d'ouverture.</param>
/// <param name="AverageTime">Durée moyenne d'une ouverture.</param>
/// <param name="LongestTime">Ouverture la plus longue.</param>
/// <param name="DistortionCount">Écrêtages de l'audio entrant signalés par SVXLink.</param>
/// <param name="ParrotTime">Temps passé sur un salon perroquet.</param>
/// <param name="ParrotSessionCount">Nombre de bascules vers un salon perroquet.</param>
/// <param name="IsSquelchTrackingObserved">
/// Au moins une ouverture de squelch a été enregistrée depuis toujours. À faux, les compteurs
/// locaux à zéro peuvent tout aussi bien signifier que SVXLink ne journalise pas le squelch
/// que l'absence réelle de trafic : l'interface doit le dire plutôt que d'afficher un zéro trompeur.
/// </param>
public record LocalActivityDto(
    int TransmissionCount,
    TimeSpan TotalTime,
    TimeSpan AverageTime,
    TimeSpan LongestTime,
    int DistortionCount,
    TimeSpan ParrotTime,
    int ParrotSessionCount,
    bool IsSquelchTrackingObserved);

/// <summary>
/// Usage d'un code DTMF, avec le sort qui lui est réservé aujourd'hui.
/// </summary>
/// <param name="Code">Code composé.</param>
/// <param name="Label">Action correspondante.</param>
/// <param name="Category">Catégorie du code.</param>
/// <param name="Count">Nombre de compositions.</param>
/// <param name="LastUsedAt">Dernière composition.</param>
public record DtmfCodeUsageDto(
    string Code,
    string Label,
    DtmfCommandCategory Category,
    int Count,
    DateTimeOffset LastUsedAt);

/// <summary>
/// Répartition des commandes DTMF reçues.
/// </summary>
/// <param name="TotalCount">Nombre total de codes composés.</param>
/// <param name="CountsByCategory">Nombre de codes par catégorie, ordonné par volume.</param>
/// <param name="TopCodes">Palmarès des codes composés.</param>
/// <param name="UnmatchedCodes">Codes sans destinataire, à examiner en priorité.</param>
/// <param name="LastUsedAt">Dernière commande reçue.</param>
public record DtmfStatisticsDto(
    int TotalCount,
    IReadOnlyList<(DtmfCommandCategory Category, int Count)> CountsByCategory,
    IReadOnlyList<DtmfCodeUsageDto> TopCodes,
    IReadOnlyList<DtmfCodeUsageDto> UnmatchedCodes,
    DateTimeOffset? LastUsedAt);

/// <summary>
/// Tenue de la liaison réflecteur sur la période.
/// </summary>
/// <param name="LinkedTime">Temps de liaison effective.</param>
/// <param name="ReflectorSessionTime">Temps passé sur un salon réflecteur, dénominateur de la disponibilité.</param>
/// <param name="AvailabilityPercent">Part liée, <c>null</c> si aucun salon réflecteur n'a tourné.</param>
/// <param name="DisconnectionCount">Liaisons perdues après avoir été établies.</param>
/// <param name="FailureCount">Tentatives de liaison qui n'ont jamais abouti.</param>
/// <param name="LongestOutage">Plus longue interruption refermée sur la période.</param>
/// <param name="LongestLinkedStretch">Plus longue liaison ininterrompue.</param>
/// <param name="ApplicationStartCount">Démarrages de l'application, chacun redémarrant SVXLink.</param>
public record ReliabilityDto(
    TimeSpan LinkedTime,
    TimeSpan ReflectorSessionTime,
    double? AvailabilityPercent,
    int DisconnectionCount,
    int FailureCount,
    TimeSpan LongestOutage,
    TimeSpan LongestLinkedStretch,
    int ApplicationStartCount);

/// <summary>
/// Ligne de la chronologie.
/// </summary>
/// <param name="OccurredAt">Instant de l'événement.</param>
/// <param name="Type">Nature de l'événement.</param>
/// <param name="Label">Description lisible.</param>
/// <param name="SalonName">Salon concerné, s'il y en a un.</param>
/// <param name="Duration">Durée, pour les événements qui en ont une.</param>
public record TimelineEntryDto(
    DateTimeOffset OccurredAt,
    ActivityEventType Type,
    string Label,
    string? SalonName,
    TimeSpan? Duration);

/// <summary>
/// Historique d'activité agrégé pour la page Statistiques.
///
/// Chaque section est indépendante : une section sans donnée est rendue vide, jamais en erreur.
/// Toutes les dates sont exprimées en heure locale de la machine, prêtes à l'affichage.
/// </summary>
/// <param name="CollectedAt">Instant de la collecte.</param>
/// <param name="Period">Fenêtre demandée.</param>
/// <param name="PeriodStart">Début effectif de la fenêtre, <c>null</c> si l'historique est vide.</param>
/// <param name="HistoryStart">Plus ancien enregistrement conservé.</param>
/// <param name="RetentionDays">Durée de conservation configurée.</param>
/// <param name="Summary">Compteurs de synthèse.</param>
/// <param name="SalonUsage">Temps passé par salon, du plus occupé au moins occupé.</param>
/// <param name="UnusedSalonNames">Salons configurés jamais activés sur la période.</param>
/// <param name="ActivationOrigins">Ventilation des activations par origine.</param>
/// <param name="Traffic">Trafic distant entendu.</param>
/// <param name="HourlyActivity">Répartition horaire des passages, en heure locale.</param>
/// <param name="LocalActivity">Activité radio locale.</param>
/// <param name="Dtmf">Commandes DTMF reçues.</param>
/// <param name="Reliability">Tenue de la liaison réflecteur.</param>
/// <param name="Timeline">Derniers événements, du plus récent au plus ancien.</param>
public record StatisticsDto(
    DateTimeOffset CollectedAt,
    StatisticsPeriod Period,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? HistoryStart,
    int RetentionDays,
    StatisticsSummaryDto Summary,
    IReadOnlyList<SalonUsageDto> SalonUsage,
    IReadOnlyList<string> UnusedSalonNames,
    IReadOnlyList<ActivationOriginDto> ActivationOrigins,
    TrafficDto Traffic,
    IReadOnlyList<HourlyActivityCell> HourlyActivity,
    LocalActivityDto LocalActivity,
    DtmfStatisticsDto Dtmf,
    ReliabilityDto Reliability,
    IReadOnlyList<TimelineEntryDto> Timeline)
{
    /// <summary>Indique qu'aucune activité n'a été enregistrée sur la période.</summary>
    public bool IsEmpty =>
        SalonUsage.Count == 0 &&
        Traffic.Count == 0 &&
        Dtmf.TotalCount == 0 &&
        Timeline.Count == 0;
}
