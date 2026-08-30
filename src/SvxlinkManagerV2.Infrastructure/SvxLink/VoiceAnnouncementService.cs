using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Implémentation de <see cref="IVoiceAnnouncementService"/> : synthétise le texte
/// dans un WAV temporaire puis demande à SVXLink de le jouer via la commande DTMF
/// interne 399 (interceptée par Logic.tcl).
///
/// Enregistré en singleton : le fichier WAV de sortie est partagé, un sémaphore
/// sérialise donc les annonces concurrentes.
/// </summary>
public class VoiceAnnouncementService : IVoiceAnnouncementService
{
    private readonly ITtsService _ttsService;
    private readonly IDtmfPtyWriter _ptyWriter;
    private readonly ILogger<VoiceAnnouncementService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>Code interne de déclenchement TTS côté SVXLink (jamais exposé aux opérateurs).</summary>
    internal const int TtsInternalCode = 399;

    /// <summary>Chemin du fichier WAV temporaire produit par le TTS.</summary>
    internal const string TtsWavPath = "/tmp/svxlink_tts.wav";

    public VoiceAnnouncementService(
        ITtsService ttsService,
        IDtmfPtyWriter ptyWriter,
        ILogger<VoiceAnnouncementService> logger)
    {
        _ttsService = ttsService;
        _ptyWriter = ptyWriter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, Unit>> AnnounceAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Validation<Error, Unit>.Fail(Seq1(Error.New("Le texte de l'annonce ne peut pas être vide.")));

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Annonce vocale : « {Text} »", text);

            var ttsResult = await _ttsService.GenerateWavAsync(text, TtsWavPath, cancellationToken);
            if (ttsResult.IsFail)
            {
                _logger.LogWarning("Échec de la synthèse TTS pour l'annonce « {Text} »", text);
                return ttsResult.Map(_ => Unit.Default);
            }

            var ptyResult = await _ptyWriter.SendCommandAsync(TtsInternalCode.ToString(), cancellationToken);
            if (ptyResult.IsFail)
                _logger.LogWarning("Échec de l'envoi de la commande PTY pour l'annonce « {Text} »", text);

            return ptyResult;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
