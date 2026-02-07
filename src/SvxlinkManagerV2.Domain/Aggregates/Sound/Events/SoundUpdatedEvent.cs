using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Aggregates.Sound.Events;

/// <summary>
/// Événement émis lors de la mise à jour d'un Sound
/// </summary>
public record SoundUpdatedEvent : DomainEvent
{
    /// <summary>
    /// Identifiant unique du Sound
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nouveau nom du fichier (optionnel)
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Nouveau contenu du fichier audio (optionnel)
    /// </summary>
    public byte[]? FileContent { get; init; }

    /// <summary>
    /// Nouvelle durée (si FileContent est fourni)
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Nouveau sample rate (si FileContent est fourni)
    /// </summary>
    public int? SampleRate { get; init; }

    /// <summary>
    /// Nouveau nombre de canaux (si FileContent est fourni)
    /// </summary>
    public int? Channels { get; init; }

    /// <summary>
    /// Constructeur
    /// </summary>
    public SoundUpdatedEvent(
        Guid id,
        string? name = null,
        byte[]? fileContent = null,
        TimeSpan? duration = null,
        int? sampleRate = null,
        int? channels = null)
    {
        Id = id;
        Name = name;
        FileContent = fileContent;
        Duration = duration;
        SampleRate = sampleRate;
        Channels = channels;
    }
}
