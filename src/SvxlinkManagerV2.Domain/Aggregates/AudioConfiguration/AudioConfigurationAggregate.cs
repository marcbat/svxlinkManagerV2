using LanguageExt;
using SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration.Events;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration;

/// <summary>
/// Aggregate représentant les niveaux ALSA mémorisés par l'application (une seule instance, ID fixe).
///
/// Les niveaux vivent d'abord dans la carte son : l'aggregate n'en garde qu'une copie, afin de les
/// réappliquer au démarrage de l'application. Le nom du contrôle ALSA est mémorisé avec sa valeur —
/// une valeur n'a de sens que pour le contrôle dont elle provient, les plages variant d'un contrôle
/// à l'autre (0-31 pour « Line Out », 0-7 pour « ADC Gain » sur le codec H3).
/// </summary>
public class AudioConfigurationAggregate : AggregateRoot
{
    /// <summary>
    /// ID fixe de la configuration audio (une seule instance par application).
    /// </summary>
    public static readonly Guid FixedId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    /// <summary>
    /// Nom du contrôle ALSA de capture (audio venant du récepteur), ex. « ADC Gain ».
    /// </summary>
    public string CaptureControl { get; private set; } = string.Empty;

    /// <summary>
    /// Niveau brut du contrôle de capture, exprimé dans l'échelle propre au contrôle.
    /// </summary>
    public int CaptureLevel { get; private set; }

    /// <summary>
    /// Nom du contrôle ALSA de restitution (audio partant vers l'émetteur), ex. « Line Out ».
    /// </summary>
    public string PlaybackControl { get; private set; } = string.Empty;

    /// <summary>
    /// Niveau brut du contrôle de restitution, exprimé dans l'échelle propre au contrôle.
    /// </summary>
    public int PlaybackLevel { get; private set; }

    /// <summary>
    /// Constructeur par défaut requis pour la réhydratation EF Core.
    /// </summary>
    public AudioConfigurationAggregate() { }

    /// <summary>
    /// Crée la configuration audio avec l'ID fixe.
    /// </summary>
    /// <param name="captureControl">Nom du contrôle ALSA de capture</param>
    /// <param name="captureLevel">Niveau brut de capture</param>
    /// <param name="playbackControl">Nom du contrôle ALSA de restitution</param>
    /// <param name="playbackLevel">Niveau brut de restitution</param>
    public static Validation<Error, AudioConfigurationAggregate> Create(
        string captureControl,
        int captureLevel,
        string playbackControl,
        int playbackLevel)
    {
        return (ValidateControl(captureControl, "AUDIO_CAPTURE_CONTROL_REQUIRED", "de capture"),
                ValidateLevel(captureLevel, "AUDIO_CAPTURE_LEVEL_INVALID", "de capture"),
                ValidateControl(playbackControl, "AUDIO_PLAYBACK_CONTROL_REQUIRED", "de restitution"),
                ValidateLevel(playbackLevel, "AUDIO_PLAYBACK_LEVEL_INVALID", "de restitution"))
            .Apply((validCaptureControl, validCaptureLevel, validPlaybackControl, validPlaybackLevel) =>
            {
                var aggregate = new AudioConfigurationAggregate();
                var @event = new AudioConfigurationCreated(
                    FixedId,
                    validCaptureControl,
                    validCaptureLevel,
                    validPlaybackControl,
                    validPlaybackLevel);

                aggregate.Apply(@event);
                aggregate.AddDomainEvent(@event);

                return aggregate;
            });
    }

    /// <summary>
    /// Met à jour les niveaux mémorisés.
    /// </summary>
    /// <param name="captureControl">Nom du contrôle ALSA de capture</param>
    /// <param name="captureLevel">Niveau brut de capture</param>
    /// <param name="playbackControl">Nom du contrôle ALSA de restitution</param>
    /// <param name="playbackLevel">Niveau brut de restitution</param>
    public Validation<Error, Unit> UpdateLevels(
        string captureControl,
        int captureLevel,
        string playbackControl,
        int playbackLevel)
    {
        return (ValidateControl(captureControl, "AUDIO_CAPTURE_CONTROL_REQUIRED", "de capture"),
                ValidateLevel(captureLevel, "AUDIO_CAPTURE_LEVEL_INVALID", "de capture"),
                ValidateControl(playbackControl, "AUDIO_PLAYBACK_CONTROL_REQUIRED", "de restitution"),
                ValidateLevel(playbackLevel, "AUDIO_PLAYBACK_LEVEL_INVALID", "de restitution"))
            .Apply((validCaptureControl, validCaptureLevel, validPlaybackControl, validPlaybackLevel) =>
            {
                var @event = new AudioLevelsUpdated(
                    validCaptureControl,
                    validCaptureLevel,
                    validPlaybackControl,
                    validPlaybackLevel);

                Apply(@event);
                AddDomainEvent(@event);

                return unit;
            });
    }

    /// <summary>
    /// Indique si les niveaux mémorisés portent bien sur les contrôles ALSA passés en paramètre.
    /// Si la configuration désigne d'autres contrôles, les valeurs mémorisées ne leur sont pas
    /// transposables et ne doivent surtout pas leur être appliquées.
    /// </summary>
    /// <param name="captureControl">Nom du contrôle ALSA de capture attendu</param>
    /// <param name="playbackControl">Nom du contrôle ALSA de restitution attendu</param>
    public bool Targets(string? captureControl, string? playbackControl) =>
        string.Equals(CaptureControl, captureControl?.Trim(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(PlaybackControl, playbackControl?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Un contrôle ALSA est désigné par son nom : sans nom, la valeur mémorisée ne s'applique à rien.
    /// </summary>
    private static Validation<Error, string> ValidateControl(string control, string code, string role) =>
        string.IsNullOrWhiteSpace(control)
            ? Error.Validation(code, $"Le nom du contrôle ALSA {role} est obligatoire.").ToFailure<string>()
            : control.Trim().ToSuccess();

    /// <summary>
    /// Seule l'absurdité manifeste est rejetée ici : la borne haute appartient à la carte son,
    /// pas au domaine, et c'est le service ALSA qui borne à la plage réelle du contrôle.
    /// </summary>
    private static Validation<Error, int> ValidateLevel(int level, string code, string role) =>
        level < 0
            ? Error.Validation(code, $"Le niveau {role} ne peut pas être négatif.").ToFailure<int>()
            : level.ToSuccess();

    #region Apply

    /// <summary>
    /// Applique l'événement <see cref="AudioConfigurationCreated"/>.
    /// </summary>
    /// <param name="event">Événement de création</param>
    public void Apply(AudioConfigurationCreated @event)
    {
        Id = @event.Id;
        CaptureControl = @event.CaptureControl;
        CaptureLevel = @event.CaptureLevel;
        PlaybackControl = @event.PlaybackControl;
        PlaybackLevel = @event.PlaybackLevel;
    }

    /// <summary>
    /// Applique l'événement <see cref="AudioLevelsUpdated"/>.
    /// </summary>
    /// <param name="event">Événement de mise à jour</param>
    public void Apply(AudioLevelsUpdated @event)
    {
        CaptureControl = @event.CaptureControl;
        CaptureLevel = @event.CaptureLevel;
        PlaybackControl = @event.PlaybackControl;
        PlaybackLevel = @event.PlaybackLevel;
    }

    #endregion
}
