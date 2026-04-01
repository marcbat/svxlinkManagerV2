using System.Reflection;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service de déploiement du fichier Logic.tcl vers le répertoire d'événements SVXLink local.
/// Le fichier Logic.tcl est embarqué dans l'assembly Infrastructure comme ressource.
/// Il surcharge proc startup {} pour jouer Name.wav une seule fois au démarrage du daemon.
/// Compatible SVXLink 19.09.2.
/// </summary>
public class LogicTclDeploymentService : ILogicTclDeploymentService
{
    private readonly ILogger<LogicTclDeploymentService> _logger;
    private readonly string _targetDirectory;

    private const string DefaultTargetDirectory = "/usr/share/svxlink/events.d/local";
    private const string LogicTclFileName = "Logic.tcl";
    private const string EmbeddedResourceName = "SvxlinkManagerV2.Infrastructure.SvxLink.Resources.Logic.tcl";

    // Constructeur pour l'injection de dépendances
    public LogicTclDeploymentService(ILogger<LogicTclDeploymentService> logger)
        : this(logger, null) { }

    // Constructeur complet pour les tests (passage du répertoire cible)
    public LogicTclDeploymentService(
        ILogger<LogicTclDeploymentService> logger,
        string? targetDirectory)
    {
        _logger = logger;
        _targetDirectory = string.IsNullOrEmpty(targetDirectory)
            ? DefaultTargetDirectory
            : targetDirectory;
    }

    /// <inheritdoc />
    public async Task<Validation<Error, Unit>> DeployAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Déploiement du Logic.tcl vers {TargetDirectory}", _targetDirectory);

            // 1. Lire la ressource embarquée
            var assembly = Assembly.GetExecutingAssembly();
            using var resourceStream = assembly.GetManifestResourceStream(EmbeddedResourceName);
            if (resourceStream is null)
            {
                var error = Error.New(
                    $"Ressource embarquée introuvable: {EmbeddedResourceName}");
                _logger.LogError("Ressource Logic.tcl introuvable dans l'assembly");
                return Validation<Error, Unit>.Fail(Seq1(error));
            }

            using var reader = new StreamReader(resourceStream);
            var content = await reader.ReadToEndAsync(cancellationToken);

            // 2. S'assurer que le répertoire cible existe
            if (!Directory.Exists(_targetDirectory))
            {
                Directory.CreateDirectory(_targetDirectory);
                _logger.LogDebug("Répertoire cible créé: {Directory}", _targetDirectory);
            }

            // 3. Écrire le fichier Logic.tcl (écrasement atomique via tmp + rename)
            var targetPath = Path.Combine(_targetDirectory, LogicTclFileName);
            var tempPath = $"{targetPath}.tmp";

            try
            {
                await File.WriteAllTextAsync(tempPath, content, cancellationToken);
                File.Move(tempPath, targetPath, overwrite: true);
            }
            catch
            {
                // Nettoyage du fichier temporaire en cas d'erreur
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }

            _logger.LogInformation(
                "Logic.tcl déployé avec succès: {TargetPath}", targetPath);

            return Success<Error, Unit>(unit);
        }
        catch (Exception ex)
        {
            var error = Error.New($"Erreur lors du déploiement du Logic.tcl: {ex.Message}", ex);
            _logger.LogError(ex, "Exception lors du déploiement du Logic.tcl");
            return Validation<Error, Unit>.Fail(Seq1(error));
        }
    }
}
