namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Suivi des ouvertures du squelch du récepteur local, déduit du flux de logs SVXLink.
///
/// C'est la seule source d'activité radio locale dont dispose l'application : le périphérique
/// de capture ALSA étant ouvert en exclusivité par SVXLink dès qu'un salon tourne, aucune mesure
/// directe n'est possible. C'est aussi la seule source disponible en mode autonome et sur un
/// salon perroquet, où il n'existe pas de liaison réflecteur pour rapporter les passages.
///
/// Le motif exploité (<c>Rx1: The squelch is OPEN</c> / <c>CLOSED</c>) dépend du niveau de
/// verbosité de SVXLink : son absence se traduit par une statistique locale à zéro, jamais
/// par une erreur. Singleton, thread-safe, sans persistance.
/// </summary>
public interface ISquelchStateService
{
    /// <summary>Indique que le squelch est actuellement ouvert.</summary>
    bool IsOpen { get; }

    /// <summary>Déclenché à l'ouverture du squelch.</summary>
    event Action<DateTimeOffset>? OnSquelchOpened;

    /// <summary>
    /// Déclenché à la fermeture du squelch, avec la durée pendant laquelle il est resté ouvert.
    /// Une fermeture sans ouverture connue n'émet rien.
    /// </summary>
    event Action<TimeSpan>? OnSquelchClosed;
}
