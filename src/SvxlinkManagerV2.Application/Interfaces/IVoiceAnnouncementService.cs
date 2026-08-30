using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service d'annonce vocale sur la fréquence : synthétise un texte puis déclenche
/// sa lecture par SVXLink (via la commande DTMF interne 399).
/// Encapsule l'enchaînement TTS + écriture PTY et sérialise les annonces concurrentes.
/// </summary>
public interface IVoiceAnnouncementService
{
    /// <summary>
    /// Synthétise le texte fourni et déclenche sa lecture sur la fréquence.
    /// </summary>
    /// <param name="text">Texte en français à annoncer.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Unit en cas de succès, ou une erreur si le TTS ou le PTY a échoué.</returns>
    Task<Validation<Error, Unit>> AnnounceAsync(string text, CancellationToken cancellationToken = default);
}
