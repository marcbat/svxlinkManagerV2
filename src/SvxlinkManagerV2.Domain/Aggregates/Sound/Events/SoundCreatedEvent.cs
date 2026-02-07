using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Sound.Events;

/// <summary>
/// Événement émis lors de la création d'un Sound (fichier audio .wav)
/// </summary>
public record SoundCreatedEvent : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Sound
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nom du fichier audio (sans extension)
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Contenu du fichier audio (.wav)
    /// </summary>
    public byte[] FileContent { get; init; } = System.Array.Empty<byte>();

    /// <summary>
    /// Durée du fichier audio (calculée depuis le header WAV)
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Sample rate (Hz) du fichier WAV
    /// </summary>
    public int SampleRate { get; init; }

    /// <summary>
    /// Nombre de canaux audio (1 = mono, 2 = stereo)
    /// </summary>
    public int Channels { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public SoundCreatedEvent(
        Guid id,
        string name,
        byte[] fileContent,
        TimeSpan duration,
        int sampleRate,
        int channels)
    {
        Id = id;
        Name = name;
        FileContent = fileContent;
        Duration = duration;
        SampleRate = sampleRate;
        Channels = channels;
    }
}
