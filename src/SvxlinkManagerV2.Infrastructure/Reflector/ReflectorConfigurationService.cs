using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Reflector;

/// <summary>
/// Service d'écriture du fichier de configuration svxreflector.conf.
/// Écrit le contenu brut INI de l'aggregate sur le disque de façon atomique
/// (fichier temporaire + renommage).
/// </summary>
public class ReflectorConfigurationService : IReflectorConfigurationService
{
    private readonly ILogger<ReflectorConfigurationService> _logger;

    public ReflectorConfigurationService(ILogger<ReflectorConfigurationService> logger)
    {
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> WriteConfigAsync(
        ReflectorAggregate reflector,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Écriture de la configuration reflector vers {OutputPath}",
            outputPath);

        try
        {
            // Créer le répertoire cible si nécessaire
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug("Répertoire créé : {Directory}", directory);
            }

            // Écriture atomique : fichier temporaire puis renommage
            var tempPath = outputPath + ".tmp";

            await File.WriteAllTextAsync(tempPath, reflector.Config, cancellationToken);

            // Renommage atomique (remplace le fichier cible s'il existe)
            File.Move(tempPath, outputPath, overwrite: true);

            _logger.LogInformation(
                "Fichier svxreflector.conf écrit avec succès ({Bytes} octets)",
                reflector.Config.Length);

            return Success<Error, Unit>(unit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'écriture du fichier svxreflector.conf : {Path}", outputPath);
            return Validation<Error, Unit>.Fail(
                Seq1(Error.New($"Erreur lors de l'écriture de la configuration : {ex.Message}")));
        }
    }
}
