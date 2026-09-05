using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Application.Models;

/// <summary>
/// Cumul par nature d'événement sur une période.
/// </summary>
/// <param name="Type">Nature de l'événement.</param>
/// <param name="Count">Nombre d'occurrences.</param>
/// <param name="TotalSeconds">Somme des durées, nulle pour les événements sans durée.</param>
/// <param name="MaxSeconds">Durée la plus longue observée.</param>
public record ActivityEventSummary(ActivityEventType Type, int Count, long TotalSeconds, int MaxSeconds);

/// <summary>
/// Cumul d'une nature d'événement, ventilé par salon.
/// </summary>
/// <param name="SalonId">Salon concerné, <c>null</c> hors salon.</param>
/// <param name="SalonName">Nom du salon au moment des événements.</param>
/// <param name="Count">Nombre d'occurrences.</param>
/// <param name="TotalSeconds">Somme des durées.</param>
public record SalonEventSummary(Guid? SalonId, string? SalonName, int Count, long TotalSeconds);

/// <summary>
/// Trafic attribué à un indicatif entendu.
/// </summary>
/// <param name="Callsign">Indicatif du nœud distant.</param>
/// <param name="Count">Nombre de passages.</param>
/// <param name="TotalSeconds">Durée cumulée de parole.</param>
/// <param name="LastHeardAt">Dernier passage, en UTC.</param>
public record CallsignSummary(string Callsign, int Count, long TotalSeconds, DateTimeOffset LastHeardAt);

/// <summary>
/// Fréquence d'un code DTMF composé.
/// </summary>
/// <param name="Code">Code tel qu'il a été reçu.</param>
/// <param name="Count">Nombre de compositions.</param>
/// <param name="LastUsedAt">Dernière composition, en UTC.</param>
public record DtmfCodeSummary(string Code, int Count, DateTimeOffset LastUsedAt);

/// <summary>
/// Case de la répartition horaire du trafic, en heure locale figée à l'écriture.
/// </summary>
/// <param name="DayOfWeek">Jour de la semaine, de 0 (dimanche) à 6 (samedi).</param>
/// <param name="Hour">Heure locale, de 0 à 23.</param>
/// <param name="Count">Nombre de passages entendus.</param>
/// <param name="TotalSeconds">Durée cumulée de ces passages.</param>
public record HourlyActivityCell(int DayOfWeek, int Hour, int Count, long TotalSeconds);
