using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Statistics;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Façade d'écriture de l'historique d'activité, utilisable depuis un handler MediatR
/// comme depuis un service hébergé.
///
/// Elle mémorise le salon courant : les appelants qui signalent un passage ou un code DTMF
/// n'ont pas à le rappeler, l'attribution au salon est faite ici. Elle tient aussi les
/// intervalles encore ouverts — liaison réflecteur en cours, interruption en cours — puisque
/// les événements de durée ne sont écrits qu'à leur fin.
///
/// Aucune de ses méthodes ne remonte d'erreur : l'échec d'une écriture statistique ne doit
/// jamais faire échouer l'activation d'un salon ni interrompre le traitement d'une commande DTMF.
/// </summary>
public interface IActivityRecorder
{
    /// <summary>
    /// Instant d'établissement de la liaison réflecteur en cours, <c>null</c> si le nœud n'est pas lié.
    ///
    /// Une période de liaison n'est écrite qu'à sa perte, avec sa durée : sans cette lecture,
    /// un nœud lié sans interruption depuis trois jours afficherait zéro seconde de liaison
    /// et une disponibilité nulle.
    /// </summary>
    DateTimeOffset? PendingLinkUpSince { get; }

    /// <summary>
    /// Ouvre une session, en clôturant la précédente. Devient le salon de référence
    /// des événements enregistrés ensuite.
    /// </summary>
    /// <param name="salonId">Salon activé, <c>null</c> en mode autonome.</param>
    /// <param name="salonName">Nom affiché du salon.</param>
    /// <param name="kind">Nature de la session.</param>
    /// <param name="origin">Ce qui a déclenché l'activation.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task RecordSessionStartAsync(
        Guid? salonId,
        string salonName,
        SalonKind kind,
        SalonActivationOrigin origin,
        CancellationToken cancellationToken = default);

    /// <summary>Clôt la session en cours sans en ouvrir de nouvelle.</summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task CloseCurrentSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enregistre un événement ponctuel, attribué au salon de la session en cours.
    /// </summary>
    /// <param name="type">Nature de l'événement.</param>
    /// <param name="callsign">Indicatif entendu, pour un passage distant.</param>
    /// <param name="duration">Durée de l'événement, pour ceux qui en ont une.</param>
    /// <param name="detail">Complément textuel (code DTMF, cause d'une perte de liaison).</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task RecordEventAsync(
        ActivityEventType type,
        string? callsign = null,
        TimeSpan? duration = null,
        string? detail = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enregistre une transition de l'état de la liaison réflecteur et en déduit les événements
    /// de durée : fin d'une période de liaison, fin d'une interruption. C'est le recorder qui
    /// tient ces intervalles, l'appelant se contente de lui transmettre l'état publié par le tracker.
    /// </summary>
    /// <param name="state">Nouvel état de la liaison.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task RecordLinkStateAsync(ReflectorLinkState state, CancellationToken cancellationToken = default);
}
