using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de synthèse vocale (TTS — Text-To-Speech).
/// Génère un fichier WAV à partir d'un texte fourni.
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// Génère un fichier WAV à partir du texte fourni.
    /// </summary>
    /// <param name="text">Texte à synthétiser.</param>
    /// <param name="outputPath">Chemin du fichier WAV à produire.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Chemin du fichier WAV produit, ou une erreur.</returns>
    Task<Validation<Error, string>> GenerateWavAsync(string text, string outputPath, CancellationToken cancellationToken = default);
}
