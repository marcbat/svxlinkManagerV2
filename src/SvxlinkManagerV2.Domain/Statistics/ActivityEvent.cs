namespace SvxlinkManagerV2.Domain.Statistics;

/// <summary>
/// Événement ponctuel de l'historique d'activité : un passage entendu, une commande DTMF,
/// une bascule de liaison réflecteur. Table en ajout seul, purgée par ancienneté.
/// </summary>
public class ActivityEvent
{
    /// <summary>Identifiant technique de l'événement.</summary>
    public Guid Id { get; private set; }

    /// <summary>Instant de l'événement, en UTC.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    /// Heure locale (0-23) au moment de l'enregistrement, figée à l'écriture.
    ///
    /// La répartition horaire n'a de sens que dans le fuseau de l'opérateur, et SQLite ne sait
    /// pas convertir un <see cref="DateTimeOffset"/> en heure locale dans une requête agrégée.
    /// Figer l'heure à l'écriture rend le regroupement trivial et applique l'offset réellement
    /// en vigueur ce jour-là, heure d'été comprise.
    /// </summary>
    public int LocalHour { get; private set; }

    /// <summary>Jour de la semaine local, de 0 (dimanche) à 6 (samedi), figé à l'écriture.</summary>
    public int LocalDayOfWeek { get; private set; }

    /// <summary>Nature de l'événement.</summary>
    public ActivityEventType Type { get; private set; }

    /// <summary>Salon sur lequel le nœud était posé, <c>null</c> hors salon.</summary>
    public Guid? SalonId { get; private set; }

    /// <summary>Nom du salon au moment de l'événement, recopié pour survivre à sa suppression.</summary>
    public string? SalonName { get; private set; }

    /// <summary>Indicatif du nœud entendu, renseigné pour les passages distants uniquement.</summary>
    public string? Callsign { get; private set; }

    /// <summary>Durée de l'événement en secondes, pour les événements qui en ont une.</summary>
    public int? DurationSeconds { get; private set; }

    /// <summary>Complément textuel : code DTMF composé, cause d'une perte de liaison.</summary>
    public string? Detail { get; private set; }

    private ActivityEvent() { }

    /// <summary>
    /// Crée un événement daté. Les champs d'heure locale sont dérivés de <paramref name="occurredAt"/>
    /// converti dans le fuseau de la machine.
    /// </summary>
    /// <param name="type">Nature de l'événement.</param>
    /// <param name="occurredAt">Instant de l'événement, normalisé en UTC.</param>
    /// <param name="salonId">Salon concerné, si applicable.</param>
    /// <param name="salonName">Nom du salon concerné, si applicable.</param>
    /// <param name="callsign">Indicatif entendu, pour un passage distant.</param>
    /// <param name="duration">Durée de l'événement, pour ceux qui en ont une.</param>
    /// <param name="detail">Complément textuel.</param>
    public static ActivityEvent Create(
        ActivityEventType type,
        DateTimeOffset occurredAt,
        Guid? salonId = null,
        string? salonName = null,
        string? callsign = null,
        TimeSpan? duration = null,
        string? detail = null)
    {
        var local = occurredAt.ToLocalTime();

        return new ActivityEvent
        {
            Id = Guid.NewGuid(),
            OccurredAt = occurredAt.ToUniversalTime(),
            LocalHour = local.Hour,
            LocalDayOfWeek = (int)local.DayOfWeek,
            Type = type,
            SalonId = salonId,
            SalonName = salonName,
            Callsign = string.IsNullOrWhiteSpace(callsign) ? null : callsign.Trim(),
            // Une durée négative n'a pas de sens et fausserait tous les cumuls : elle est ramenée à zéro.
            DurationSeconds = duration is { } d ? (int)Math.Max(0, Math.Round(d.TotalSeconds)) : null,
            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim()
        };
    }
}
