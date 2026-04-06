using LanguageExt;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service de génération de l'annonce sonore du salon via pico2wave.
/// Génère Name.wav à partir du nom du salon à l'activation.
/// Compatible avec SVXLink 19.09.2.
/// </summary>
public class SalonAnnouncementService : ISalonAnnouncementService
{
    private readonly ITtsService _ttsService;
    private readonly ILogger<SalonAnnouncementService> _logger;
    private readonly string _deployDirectory;

    private const string DefaultDeployDirectory = "/usr/share/svxlink/sounds/fr_FR/svxlinkmanager";
    private const string AnnounceFileName = "Name.wav";

    public SalonAnnouncementService(ITtsService ttsService, ILogger<SalonAnnouncementService> logger)
        : this(ttsService, logger, null) { }

    public SalonAnnouncementService(ITtsService ttsService, ILogger<SalonAnnouncementService> logger, string? deployDirectory)
    {
        _ttsService = ttsService;
        _logger = logger;
        _deployDirectory = string.IsNullOrEmpty(deployDirectory) ? DefaultDeployDirectory : deployDirectory;
    }

    /// <inheritdoc />
    public async Task<Validation<Error, Unit>> GenerateAsync(string salonName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_deployDirectory))
                Directory.CreateDirectory(_deployDirectory);

            var outputPath = Path.Combine(_deployDirectory, AnnounceFileName);
            var announcementText = $"Bienvenue sur le salon {salonName}";
            _logger.LogInformation("Génération de l'annonce TTS pour le salon « {SalonName} » → {OutputPath}", salonName, outputPath);

            var result = await _ttsService.GenerateWavAsync(announcementText, outputPath, cancellationToken);

            if (result.IsFail)
            {
                var ttsErrors = result.Match(
                    Succ: _ => string.Empty,
                    Fail: errs => string.Join(", ", errs.Select(e => e.Message)));
                _logger.LogWarning("Échec de la génération TTS pour « {SalonName} » : {Errors}", salonName, ttsErrors);
                return Error.Validation("TTS_GENERATION_FAILED", $"Échec pico2wave : {ttsErrors}").ToFailure<Unit>();
            }

            _logger.LogInformation("Annonce générée avec succès : {OutputPath}", outputPath);
            return unit.ToSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de la génération de l'annonce pour « {SalonName} »", salonName);
            return Error.Validation("TTS_EXCEPTION", ex.Message).ToFailure<Unit>();
        }
    }

    /// <inheritdoc />
    public Task<Validation<Error, Unit>> CleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = Path.Combine(_deployDirectory, AnnounceFileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Fichier d'annonce supprimé : {FilePath}", filePath);
            }
            return Task.FromResult(unit.ToSuccess());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de la suppression du fichier d'annonce");
            return Task.FromResult(Error.Validation("CLEANUP_EXCEPTION", ex.Message).ToFailure<Unit>());
        }
    }
}
