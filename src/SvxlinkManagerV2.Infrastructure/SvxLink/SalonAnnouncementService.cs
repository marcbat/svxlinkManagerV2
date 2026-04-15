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
/// Déploie dans les répertoires de sons des deux versions de SVXLink.
/// </summary>
public class SalonAnnouncementService : ISalonAnnouncementService
{
    private readonly ITtsService _ttsService;
    private readonly ILogger<SalonAnnouncementService> _logger;
    private readonly IReadOnlyList<string> _deployDirectories;

    private const string DefaultDeployDirectory = "/usr/share/svxlink/sounds/fr_FR/svxlinkmanager";
    private const string AnnounceFileName = "Name.wav";

    public SalonAnnouncementService(
        ITtsService ttsService,
        ILogger<SalonAnnouncementService> logger,
        ISvxLinkStrategyResolver strategyResolver)
        : this(ttsService, logger, strategyResolver.GetAll().Select(s => s.SoundsDirectory).ToList()) { }

    public SalonAnnouncementService(
        ITtsService ttsService,
        ILogger<SalonAnnouncementService> logger,
        IReadOnlyList<string> deployDirectories)
    {
        _ttsService = ttsService;
        _logger = logger;
        _deployDirectories = deployDirectories.Count > 0
            ? deployDirectories
            : new[] { DefaultDeployDirectory };
    }

    /// <inheritdoc />
    public async Task<Validation<Error, Unit>> GenerateAsync(string salonName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Générer le WAV dans un répertoire temporaire, puis copier vers toutes les destinations
            var tempDir = Path.Combine(Path.GetTempPath(), "svxlink-tts");
            if (!Directory.Exists(tempDir))
                Directory.CreateDirectory(tempDir);

            var tempPath = Path.Combine(tempDir, AnnounceFileName);
            var announcementText = $"Bienvenue sur le {salonName}";
            _logger.LogInformation("Génération de l'annonce TTS pour le salon « {SalonName} »", salonName);

            var result = await _ttsService.GenerateWavAsync(announcementText, tempPath, cancellationToken);

            if (result.IsFail)
            {
                var ttsErrors = result.Match(
                    Succ: _ => string.Empty,
                    Fail: errs => string.Join(", ", errs.Select(e => e.Message)));
                _logger.LogWarning("Échec de la génération TTS pour « {SalonName} » : {Errors}", salonName, ttsErrors);
                return Error.Validation("TTS_GENERATION_FAILED", $"Échec pico2wave : {ttsErrors}").ToFailure<Unit>();
            }

            // Déployer dans chaque répertoire de sons
            foreach (var dir in _deployDirectories)
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var targetPath = Path.Combine(dir, AnnounceFileName);
                File.Copy(tempPath, targetPath, overwrite: true);
                _logger.LogInformation("Annonce déployée : {OutputPath}", targetPath);
            }

            // Nettoyage du fichier temporaire
            if (File.Exists(tempPath))
                File.Delete(tempPath);

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
            foreach (var dir in _deployDirectories)
            {
                var filePath = Path.Combine(dir, AnnounceFileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Fichier d'annonce supprimé : {FilePath}", filePath);
                }
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
