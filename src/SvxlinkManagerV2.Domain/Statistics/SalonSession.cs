namespace SvxlinkManagerV2.Domain.Statistics;

/// <summary>
/// Période pendant laquelle le nœud a été posé sur un salon — ou en mode autonome.
/// Ouverte à l'activation, close à l'activation suivante ou à l'arrêt de l'application.
///
/// Le nom du salon est **recopié** dans la session : renommer ou supprimer un salon ne doit
/// pas rendre illisible l'historique, et le lien vers <c>SalonAggregate</c> n'est qu'indicatif.
/// </summary>
public class SalonSession
{
    /// <summary>Identifiant technique de la session.</summary>
    public Guid Id { get; private set; }

    /// <summary>Salon concerné, <c>null</c> en mode autonome.</summary>
    public Guid? SalonId { get; private set; }

    /// <summary>Nom du salon au moment de l'activation.</summary>
    public string SalonName { get; private set; }

    /// <summary>Nature de la session (réflecteur, perroquet, autonome).</summary>
    public SalonKind Kind { get; private set; }

    /// <summary>Ce qui a déclenché l'activation.</summary>
    public SalonActivationOrigin Origin { get; private set; }

    /// <summary>Début de la session, en UTC.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Fin de la session en UTC, <c>null</c> tant qu'elle est en cours.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>
    /// Session close a posteriori au démarrage suivant, faute d'arrêt propre.
    /// Sa durée est alors bornée au dernier événement connu, pas à l'heure de la reprise :
    /// une machine restée éteinte trois semaines ne doit pas compter trois semaines d'antenne.
    /// </summary>
    public bool ClosedOnRecovery { get; private set; }

    private SalonSession()
    {
        SalonName = string.Empty;
    }

    /// <summary>Ouvre une session.</summary>
    /// <param name="salonId">Salon concerné, <c>null</c> en mode autonome.</param>
    /// <param name="salonName">Nom affiché du salon.</param>
    /// <param name="kind">Nature de la session.</param>
    /// <param name="origin">Ce qui a déclenché l'activation.</param>
    /// <param name="startedAt">Instant du début, normalisé en UTC.</param>
    public static SalonSession Start(
        Guid? salonId,
        string salonName,
        SalonKind kind,
        SalonActivationOrigin origin,
        DateTimeOffset startedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            SalonId = salonId,
            SalonName = string.IsNullOrWhiteSpace(salonName) ? "Sans nom" : salonName.Trim(),
            Kind = kind,
            Origin = origin,
            StartedAt = startedAt.ToUniversalTime()
        };

    /// <summary>
    /// Clôt la session. Une fin antérieure au début (horloge reculée, événement plus ancien
    /// que la session lors d'une reprise) est ramenée au début pour ne jamais produire de durée négative.
    /// </summary>
    /// <param name="endedAt">Instant de fin, normalisé en UTC.</param>
    /// <param name="closedOnRecovery">Clôture a posteriori après un arrêt brutal.</param>
    public void Close(DateTimeOffset endedAt, bool closedOnRecovery = false)
    {
        if (EndedAt.HasValue)
            return;

        var utc = endedAt.ToUniversalTime();
        EndedAt = utc < StartedAt ? StartedAt : utc;
        ClosedOnRecovery = closedOnRecovery;
    }

    /// <summary>Indique que la session n'a pas encore été close.</summary>
    public bool IsOpen => EndedAt is null;

    /// <summary>Durée écoulée, nulle tant que la session est ouverte.</summary>
    public TimeSpan Duration => EndedAt is { } end ? end - StartedAt : TimeSpan.Zero;
}
