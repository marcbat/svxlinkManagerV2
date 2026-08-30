using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration.Events;

/// <summary>
/// Événement émis lors de la mise à jour des niveaux ALSA mémorisés.
/// </summary>
public record AudioLevelsUpdated : DomainEvent
{
    public string CaptureControl { get; init; }
    public int CaptureLevel { get; init; }
    public string PlaybackControl { get; init; }
    public int PlaybackLevel { get; init; }

    public AudioLevelsUpdated(
        string captureControl,
        int captureLevel,
        string playbackControl,
        int playbackLevel)
    {
        CaptureControl = captureControl;
        CaptureLevel = captureLevel;
        PlaybackControl = playbackControl;
        PlaybackLevel = playbackLevel;
    }
}
