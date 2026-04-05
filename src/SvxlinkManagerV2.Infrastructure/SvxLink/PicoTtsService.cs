using System.Diagnostics;
using System.Text.RegularExpressions;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Implémentation de <see cref="ITtsService"/> utilisant pico2wave.
/// Produit un fichier WAV 16 kHz, mono, 16-bit PCM directement compatible SVXLink.
/// </summary>
public class PicoTtsService : ITtsService
{
    private readonly ILogger<PicoTtsService> _logger;
    private const string Pico2WaveExecutable = "pico2wave";
    private const string Language = "fr-FR";

    public PicoTtsService(ILogger<PicoTtsService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, string>> GenerateWavAsync(
        string text,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Validation<Error, string>.Fail(Seq1(Error.New("Le texte à synthétiser ne peut pas être vide.")));

        if (string.IsNullOrWhiteSpace(outputPath))
            return Validation<Error, string>.Fail(Seq1(Error.New("Le chemin de sortie ne peut pas être vide.")));

        var sanitizedText = SanitizeText(text);

        _logger.LogInformation("Synthèse TTS : « {Text} » → {OutputPath}", sanitizedText, outputPath);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Pico2WaveExecutable,
                ArgumentList = { "-w", outputPath, "-l", Language, sanitizedText },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                var errorMsg = $"pico2wave a échoué avec le code {process.ExitCode}. Erreur : {stderr}";
                _logger.LogError(errorMsg);
                return Validation<Error, string>.Fail(Seq1(Error.New(errorMsg)));
            }

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            {
                var errorMsg = $"pico2wave n'a pas produit de fichier valide à {outputPath}";
                _logger.LogError(errorMsg);
                return Validation<Error, string>.Fail(Seq1(Error.New(errorMsg)));
            }

            _logger.LogInformation("Fichier WAV généré avec succès : {OutputPath}", outputPath);
            return Validation<Error, string>.Success(outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de la synthèse TTS");
            return Validation<Error, string>.Fail(Seq1(Error.New(ex)));
        }
    }

    /// <summary>
    /// Sanitize le texte pour éviter toute injection de commande.
    /// Supprime les caractères de contrôle et les caractères dangereux pour le shell.
    /// </summary>
    internal static string SanitizeText(string text)
    {
        // Supprime les caractères de contrôle (retours à la ligne, tabulations, etc.)
        var sanitized = Regex.Replace(text, @"[\x00-\x1F\x7F]", " ");

        // Compresse les espaces multiples
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();

        return sanitized;
    }
}
