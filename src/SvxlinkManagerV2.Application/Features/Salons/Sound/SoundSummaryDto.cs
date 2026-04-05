namespace SvxlinkManagerV2.Application.Features.Salons.Sound;

/// <summary>
/// DTO résumé d'un son sans le contenu binaire
/// </summary>
public record SoundSummaryDto(
    Guid Id,
    string Name,
    TimeSpan Duration,
    int SampleRate,
    int Channels,
    DateTime CreatedAt);
