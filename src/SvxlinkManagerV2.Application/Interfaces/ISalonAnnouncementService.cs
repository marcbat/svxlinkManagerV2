using LanguageExt;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de génération de l'annonce sonore du salon via TTS.
/// Génère Name.wav à partir du nom du salon à l'activation.
/// </summary>
public interface ISalonAnnouncementService
{
    /// <summary>
    /// Génère le fichier Name.wav à partir du nom du salon via pico2wave.
    /// </summary>
    Task<Validation<Error, Unit>> GenerateAsync(string salonName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime le fichier Name.wav s'il existe.
    /// </summary>
    Task<Validation<Error, Unit>> CleanupAsync(CancellationToken cancellationToken = default);
}
