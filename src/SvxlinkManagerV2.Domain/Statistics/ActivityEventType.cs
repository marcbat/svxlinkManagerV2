namespace SvxlinkManagerV2.Domain.Statistics;

/// <summary>
/// Nature d'un événement ponctuel enregistré dans l'historique d'activité.
///
/// Les événements qui ont une durée (passage, liaison réflecteur) sont écrits **à leur fin**,
/// avec la durée déjà calculée : la lecture n'a alors jamais à appairer un début et une fin,
/// et un arrêt brutal de l'application ne laisse pas d'enregistrement à moitié constitué.
/// </summary>
public enum ActivityEventType
{
    /// <summary>Passage d'un nœud distant entendu sur le salon actif (durée renseignée).</summary>
    TalkerHeard = 0,

    /// <summary>Ouverture du squelch local : une station a été reçue en direct par le nœud (durée renseignée).</summary>
    LocalTransmission = 1,

    /// <summary>Commande DTMF reçue, quel que soit son sort (le code composé est dans le détail).</summary>
    DtmfCommand = 2,

    /// <summary>Période de liaison effective au réflecteur, écrite à sa perte (durée renseignée).</summary>
    ReflectorLinkUp = 3,

    /// <summary>Perte d'une liaison qui avait été établie (cause dans le détail).</summary>
    ReflectorLinkLost = 4,

    /// <summary>Échec d'établissement de la liaison (cause dans le détail).</summary>
    ReflectorLinkFailed = 5,

    /// <summary>Écrêtage de l'audio entrant signalé par SVXLink.</summary>
    RxDistortion = 6,

    /// <summary>Démarrage de l'application.</summary>
    ApplicationStarted = 7,

    /// <summary>Arrêt propre de l'application.</summary>
    ApplicationStopped = 8,

    /// <summary>
    /// Interruption de liaison, écrite au rétablissement avec la durée pendant laquelle
    /// le nœud est resté délié. Une interruption ne se connaît qu'une fois terminée :
    /// la perte, elle, est déjà enregistrée sur le moment par <see cref="ReflectorLinkLost"/>.
    /// </summary>
    ReflectorOutage = 9
}
