using System.Reflection;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service de déploiement du fichier Logic.tcl vers les répertoires d'événements SVXLink.
/// Le fichier Logic.tcl est embarqué dans l'assembly Infrastructure comme ressource.
/// Il surcharge proc startup {} pour jouer Name.wav une seule fois au démarrage du daemon.
/// Déploie dans les répertoires events.d/local des deux versions de SVXLink.
/// </summary>
public class LogicTclDeploymentService : ILogicTclDeploymentService
{
    private readonly ILogger<LogicTclDeploymentService> _logger;
    private readonly IReadOnlyList<string> _targetDirectories;

    private const string DefaultTargetDirectory = "/usr/share/svxlink/events.d/local";
    private const string LogicTclFileName = "Logic.tcl";
    private const string EmbeddedResourceName = "SvxlinkManagerV2.Infrastructure.SvxLink.Resources.Logic.tcl";

    // Constructeur pour l'injection de dépendances
    public LogicTclDeploymentService(
        ILogger<LogicTclDeploymentService> logger,
        ISvxLinkStrategyResolver strategyResolver)
        : this(logger, strategyResolver.GetAll().Select(s => s.EventsDirectory).ToList()) { }

    // Constructeur complet pour les tests (passage des répertoires cible)
    public LogicTclDeploymentService(
        ILogger<LogicTclDeploymentService> logger,
        IReadOnlyList<string> targetDirectories)
    {
        _logger = logger;
        _targetDirectories = targetDirectories.Count > 0
            ? targetDirectories
            : new[] { DefaultTargetDirectory };
    }

    /// <inheritdoc />
    public async Task<Validation<Error, Unit>> DeployAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Déploiement du Logic.tcl vers {Count} répertoire(s)", _targetDirectories.Count);

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

            // 2. Déployer dans chaque répertoire
            foreach (var targetDirectory in _targetDirectories)
            {
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                    _logger.LogDebug("Répertoire cible créé: {Directory}", targetDirectory);
                }

                var targetPath = Path.Combine(targetDirectory, LogicTclFileName);
                var tempPath = $"{targetPath}.tmp";

                try
                {
                    await File.WriteAllTextAsync(tempPath, content, cancellationToken);
                    File.Move(tempPath, targetPath, overwrite: true);
                }
                catch
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                    throw;
                }

                _logger.LogInformation(
                    "Logic.tcl déployé avec succès: {TargetPath}", targetPath);
            }

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
