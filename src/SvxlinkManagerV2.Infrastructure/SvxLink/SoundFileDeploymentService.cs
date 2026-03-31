using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service de déploiement du fichier son WAV sur le filesystem du serveur.
/// Écrit le fichier de manière atomique (pattern .tmp → rename).
/// Compatible avec SVXLink 19.09.2.
/// </summary>
public class SoundFileDeploymentService : ISoundFileDeploymentService
{
    private readonly ILogger<SoundFileDeploymentService> _logger;
    private readonly string _deployDirectory;

    private const string DefaultDeployDirectory = "/usr/share/svxlink/sounds/fr_FR/svxlinkmanager";
    private const string AnnounceFileName = "Name.wav";

    // Constructeur pour l'injection de dépendances
    public SoundFileDeploymentService(ILogger<SoundFileDeploymentService> logger)
        : this(logger, null) { }

    // Constructeur complet pour les tests (passage du répertoire cible)
    public SoundFileDeploymentService(
        ILogger<SoundFileDeploymentService> logger,
        string? deployDirectory)
    {
        _logger = logger;
        _deployDirectory = string.IsNullOrEmpty(deployDirectory)
            ? DefaultDeployDirectory
            : deployDirectory;
    }

    /// <inheritdoc />
    public async Task<Validation<Error, string>> DeployAsync(
        SoundAggregate sound,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Déploiement du fichier son '{SoundName}' (ID: {SoundId})",
                sound.Name, sound.Id);

            var deployPath = Path.Combine(_deployDirectory, AnnounceFileName);
            var tempPath = $"{deployPath}.tmp";

            // 1. S'assurer que le répertoire cible existe
            if (!Directory.Exists(_deployDirectory))
            {
                Directory.CreateDirectory(_deployDirectory);
                _logger.LogDebug("Répertoire cible créé: {Directory}", _deployDirectory);
            }

            // 2. Écrire le contenu WAV dans un fichier temporaire
            await File.WriteAllBytesAsync(tempPath, sound.FileContent, cancellationToken);
            _logger.LogDebug(
                "Fichier temporaire écrit: {TempPath} ({Length} bytes)",
                tempPath, sound.FileContent.Length);

            // 3. Remplacer l'ancien fichier atomiquement (overwrite = rename atomique sur UNIX)
            File.Move(tempPath, deployPath, overwrite: true);

            _logger.LogInformation(
                "Fichier son déployé avec succès: {DeployPath}",
                deployPath);

            return Success<Error, string>(deployPath);
        }
        catch (Exception ex)
        {
            var error = Error.New($"Erreur lors du déploiement du fichier son: {ex.Message}", ex);
            _logger.LogError(ex, "Exception lors du déploiement du fichier son pour '{SoundName}'", sound.Name);
            return Validation<Error, string>.Fail(Seq1(error));
        }
    }

    /// <inheritdoc />
    public Task<Validation<Error, Unit>> CleanupAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deployPath = Path.Combine(_deployDirectory, AnnounceFileName);

            if (File.Exists(deployPath))
            {
                File.Delete(deployPath);
                _logger.LogInformation("Fichier son supprimé: {DeployPath}", deployPath);
            }
            else
            {
                _logger.LogDebug("Aucun fichier son à supprimer: {DeployPath}", deployPath);
            }

            return Task.FromResult(Success<Error, Unit>(unit));
        }
        catch (Exception ex)
        {
            var error = Error.New($"Erreur lors du nettoyage du fichier son: {ex.Message}", ex);
            _logger.LogError(ex, "Exception lors du nettoyage du fichier son");
            return Task.FromResult(Validation<Error, Unit>.Fail(Seq1(error)));
        }
    }
}
