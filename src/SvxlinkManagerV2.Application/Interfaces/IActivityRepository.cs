using LanguageExt;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Persistance de l'historique d'activité du nœud : sessions de salon et événements ponctuels.
///
/// Les sessions se comptent en unités par jour : elles sont rendues telles quelles et agrégées
/// en mémoire. Les événements, eux, se comptent en milliers : chaque lecture est un
/// regroupement délégué à SQLite, jamais un chargement complet de la table — l'application
/// tourne sur une machine à 512 Mo de mémoire.
/// </summary>
public interface IActivityRepository
{
    /// <summary>Enregistre un événement ponctuel.</summary>
    /// <param name="activityEvent">Événement à écrire.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, Unit>> AddEventAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clôt les sessions restées ouvertes puis ouvre la nouvelle, en une seule transaction :
    /// deux sessions simultanées rendraient tous les cumuls de temps faux.
    /// </summary>
    /// <param name="session">Session à ouvrir.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, Unit>> StartSessionAsync(SalonSession session, CancellationToken cancellationToken = default);

    /// <summary>Clôt toutes les sessions restées ouvertes.</summary>
    /// <param name="endedAt">Heure de fin à inscrire.</param>
    /// <param name="closedOnRecovery">Clôture a posteriori après un arrêt brutal.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, Unit>> CloseOpenSessionsAsync(
        DateTimeOffset endedAt,
        bool closedOnRecovery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Instant du dernier signe de vie enregistré (événement ou fin de session), tous types confondus.
    /// Sert de borne de clôture aux sessions orphelines d'un arrêt brutal.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<DateTimeOffset?> GetLastActivityAtAsync(CancellationToken cancellationToken = default);

    /// <summary>Instant du plus ancien enregistrement conservé, <c>null</c> si l'historique est vide.</summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<DateTimeOffset?> GetFirstActivityAtAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indique qu'au moins un événement de cette nature a été enregistré depuis toujours,
    /// période d'observation comprise ou non. Sert à distinguer « rien ne s'est produit »
    /// de « cette source n'a jamais rien produit sur cette machine ».
    /// </summary>
    /// <param name="type">Nature d'événement recherchée.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<bool> HasAnyEventAsync(ActivityEventType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sessions qui recouvrent la période : celles closes après <paramref name="fromUtc"/>
    /// et celle éventuellement encore ouverte, y compris si elle a débuté avant.
    /// </summary>
    /// <param name="fromUtc">Début de la période, en UTC.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<IReadOnlyList<SalonSession>> GetSessionsAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken = default);

    /// <summary>Cumuls par nature d'événement depuis <paramref name="fromUtc"/>.</summary>
    /// <param name="fromUtc">Début de la période, en UTC.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<IReadOnlyList<ActivityEventSummary>> GetEventSummariesAsync(
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Cumuls d'une nature d'événement, ventilés par salon.</summary>
    /// <param name="fromUtc">Début de la période, en UTC.</param>
    /// <param name="type">Nature d'événement à ventiler.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<IReadOnlyList<SalonEventSummary>> GetSalonEventSummariesAsync(
        DateTimeOffset fromUtc,
        ActivityEventType type,
        CancellationToken cancellationToken = default);

    /// <summary>Indicatifs les plus entendus, du plus bavard au moins bavard.</summary>
    /// <param name="fromUtc">Début de la période, en UTC.</param>
    /// <param name="limit">Nombre maximal d'indicatifs rendus.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<IReadOnlyList<CallsignSummary>> GetTopCallsignsAsync(
        DateTimeOffset fromUtc,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Nombre d'indicatifs distincts entendus sur la période.</summary>
    /// <param name="fromUtc">Début de la période, en UTC.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<int> GetDistinctCallsignCountAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken = default);

    /// <summary>Fréquence de chaque code DTMF composé sur la période.</summary>
    /// <param name="fromUtc">Début de la période, en UTC.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<IReadOnlyList<DtmfCodeSummary>> GetDtmfSummariesAsync(
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Répartition des passages entendus par jour de semaine et heure locale.</summary>
    /// <param name="fromUtc">Début de la période, en UTC.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<IReadOnlyList<HourlyActivityCell>> GetHourlyActivityAsync(
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Derniers événements de la période, du plus récent au plus ancien.</summary>
    /// <param name="fromUtc">Début de la période, en UTC.</param>
    /// <param name="limit">Nombre maximal d'événements rendus.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<IReadOnlyList<ActivityEvent>> GetRecentEventsAsync(
        DateTimeOffset fromUtc,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Supprime les enregistrements antérieurs à la date de rétention.</summary>
    /// <param name="cutoffUtc">Date de coupure, en UTC.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Nombre de lignes supprimées.</returns>
    Task<Validation<Error, int>> PurgeBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);

    /// <summary>Vide entièrement l'historique.</summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, Unit>> ResetAsync(CancellationToken cancellationToken = default);
}
