namespace SvxlinkManagerV2.Application.Models;

/// <summary>
/// État d'un contrôle de mixage ALSA, tel que rapporté par <c>amixer</c>.
/// </summary>
/// <param name="Name">Nom du contrôle simple ALSA (ex. « Line Out »).</param>
/// <param name="Value">Valeur brute courante, dans l'échelle propre au contrôle.</param>
/// <param name="MinValue">Borne basse de l'échelle du contrôle.</param>
/// <param name="MaxValue">Borne haute de l'échelle du contrôle.</param>
public record AudioControlState(string Name, int Value, int MinValue, int MaxValue)
{
    /// <summary>
    /// Position du niveau courant dans l'échelle du contrôle, en pourcentage.
    /// </summary>
    public int Percent => MaxValue > MinValue
        ? (int)Math.Round((Value - MinValue) * 100.0 / (MaxValue - MinValue))
        : 0;
}

/// <summary>
/// État des deux contrôles ALSA pilotés par l'application : capture (audio venant du récepteur)
/// et restitution (audio partant vers l'émetteur).
/// </summary>
/// <param name="CardIndex">Index de la carte son interrogée.</param>
/// <param name="Capture">Contrôle de capture (gain micro / ligne en réception).</param>
/// <param name="Playback">Contrôle de restitution (niveau de sortie en émission).</param>
/// <param name="IsSimulated">Vrai lorsque les valeurs proviennent du mock de développement.</param>
public record AudioMixerState(
    int CardIndex,
    AudioControlState Capture,
    AudioControlState Playback,
    bool IsSimulated);

/// <summary>
/// État du test d'émission (PTT) déclenché depuis l'interface.
/// </summary>
/// <param name="IsTransmitting">Vrai tant que le PTT est maintenu.</param>
/// <param name="EndsAt">Instant de relâchement automatique, null hors test.</param>
/// <param name="IsSimulated">Vrai lorsque le PTT est simulé (développement sans matériel).</param>
public record PttTestState(bool IsTransmitting, DateTimeOffset? EndsAt, bool IsSimulated)
{
    /// <summary>
    /// État au repos, PTT relâché.
    /// </summary>
    public static PttTestState Idle(bool isSimulated) => new(false, null, isSimulated);

    /// <summary>
    /// Secondes restantes avant le relâchement automatique, 0 hors test.
    /// </summary>
    public int RemainingSeconds => EndsAt is null
        ? 0
        : Math.Max(0, (int)Math.Ceiling((EndsAt.Value - DateTimeOffset.UtcNow).TotalSeconds));
}
