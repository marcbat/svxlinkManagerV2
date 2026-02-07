using SvxlinkManagerV2.Domain.Aggregates.Sound.Events;

namespace SvxlinkManagerV2.Infrastructure.Persistence.Projections;

/// <summary>
/// Projection Marten pour Sound.
/// Utilisée pour les queries performantes sans rehydrater tout l'aggregate.
/// </summary>
public class SoundProjection
{
    /// <summary>
    /// Identifiant du Sound
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Nom du fichier audio
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Taille du fichier en bytes
    /// </summary>
    public int FileSizeBytes { get; set; }

    /// <summary>
    /// Durée du fichier audio
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Sample rate en Hz
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// Nombre de canaux (1 = mono, 2 = stereo)
    /// </summary>
    public int Channels { get; set; }

    /// <summary>
    /// Date de création
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Date de dernière modification
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Indique si le sound est supprimé
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Applique l'événement SoundCreatedEvent
    /// </summary>
    public void Apply(SoundCreatedEvent @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        FileSizeBytes = @event.FileContent.Length;
        Duration = @event.Duration;
        SampleRate = @event.SampleRate;
        Channels = @event.Channels;
        CreatedAt = @event.OccurredOn;
        UpdatedAt = @event.OccurredOn;
        IsDeleted = false;
    }

    /// <summary>
    /// Applique l'événement SoundUpdatedEvent
    /// </summary>
    public void Apply(SoundUpdatedEvent @event)
    {
        if (@event.Name != null)
            Name = @event.Name;

        if (@event.FileContent != null)
        {
            FileSizeBytes = @event.FileContent.Length;
            Duration = @event.Duration!.Value;
            SampleRate = @event.SampleRate!.Value;
            Channels = @event.Channels!.Value;
        }

        UpdatedAt = @event.OccurredOn;
    }

    /// <summary>
    /// Applique l'événement SoundDeletedEvent
    /// </summary>
    public void Apply(SoundDeletedEvent @event)
    {
        IsDeleted = true;
    }
}
