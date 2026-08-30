using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration.Events;

/// <summary>
/// Événement émis lors de la création de la configuration audio.
/// </summary>
public record AudioConfigurationCreated : DomainEvent
{
    public Guid Id { get; init; }
    public string CaptureControl { get; init; }
    public int CaptureLevel { get; init; }
    public string PlaybackControl { get; init; }
    public int PlaybackLevel { get; init; }

    public AudioConfigurationCreated(
        Guid id,
        string captureControl,
        int captureLevel,
        string playbackControl,
        int playbackLevel)
    {
        Id = id;
        CaptureControl = captureControl;
        CaptureLevel = captureLevel;
        PlaybackControl = playbackControl;
        PlaybackLevel = playbackLevel;
    }
}
