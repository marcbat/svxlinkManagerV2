using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Infrastructure.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service de génération du fichier de configuration SVXLink pour le mode Perroquet (Parrot).
/// Génère une configuration SVXLink avec SimplexLogic + ModuleParrot uniquement.
/// Les sections [ReflectorLogic] et [LinkToReflector] sont absentes.
/// Compatible avec SVXLink 19.09.2.
/// </summary>
public class SvxLinkParrotConfigurationService : ISvxLinkParrotConfigurationService
{
    private readonly ILogger<SvxLinkParrotConfigurationService> _logger;
    private readonly string? _templatePath;
    private const string TemplateFileName = "svxlink.conf";
    private const string SvxLinkConfigDir = "/etc/svxlink";

    // Constructeur pour l'injection de dépendances
    public SvxLinkParrotConfigurationService(ILogger<SvxLinkParrotConfigurationService> logger)
        : this(logger, null) { }

    // Constructeur complet pour les tests (passage du chemin du template)
    public SvxLinkParrotConfigurationService(
        ILogger<SvxLinkParrotConfigurationService> logger,
        string? templatePath)
    {
        _logger = logger;
        _templatePath = templatePath;
    }

    /// <inheritdoc />
    public async Task<Validation<Error, Unit>> GenerateAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Génération de la configuration SVXLink pour le mode Perroquet");

            // 1. Localiser et charger le template
            var templatePath = GetTemplatePath();
            if (!File.Exists(templatePath))
            {
                var error = Error.New($"Le fichier template '{templatePath}' est introuvable");
                _logger.LogError("Template non trouvé: {TemplatePath}", templatePath);
                return Validation<Error, Unit>.Fail(Seq1(error));
            }

            // 2. Charger le template INI
            var iniData = await Task.Run(() => IniFile.Parse(templatePath), cancellationToken);

            // 3. Configurer le mode Perroquet
            UpdateGlobalSection(iniData);
            UpdateSimplexLogicSection(iniData);
            RemoveReflectorSections(iniData);

            // 4. Écrire le fichier de manière atomique (temp + rename)
            var writeResult = await WriteConfigurationAtomicallyAsync(iniData, outputPath, cancellationToken);

            return writeResult.Match(
                Succ: _ =>
                {
                    _logger.LogInformation("Configuration Perroquet générée avec succès: {OutputPath}", outputPath);
                    return Success<Error, Unit>(unit);
                },
                Fail: errors =>
                {
                    _logger.LogError("Échec de l'écriture de la configuration Perroquet: {Errors}", errors);
                    return Validation<Error, Unit>.Fail(errors);
                });
        }
        catch (Exception ex)
        {
            var error = Error.New($"Erreur lors de la génération de la configuration Perroquet: {ex.Message}", ex);
            _logger.LogError(ex, "Exception lors de la génération de la configuration SVXLink Perroquet");
            return Validation<Error, Unit>.Fail(Seq1(error));
        }
    }

    /// <summary>
    /// Met à jour la section [GLOBAL] pour le mode Perroquet.
    /// Seul SimplexLogic est actif — pas de ReflectorLogic ni de liaison.
    /// </summary>
    private void UpdateGlobalSection(IniFile iniData)
    {
        iniData["GLOBAL"]["LOGICS"] = "SimplexLogic";
        iniData["GLOBAL"]["LINKS"] = "";
        _logger.LogDebug("Section [GLOBAL] mise à jour pour le mode Perroquet (LOGICS=SimplexLogic, LINKS=)");
    }

    /// <summary>
    /// Met à jour la section [SimplexLogic] pour activer ModuleParrot.
    /// Les autres paramètres (CALLSIGN, intervalles, etc.) sont conservés du template.
    /// </summary>
    private void UpdateSimplexLogicSection(IniFile iniData)
    {
        iniData["SimplexLogic"]["MODULES"] = "ModuleParrot";
        _logger.LogDebug("Section [SimplexLogic] mise à jour pour le mode Perroquet (MODULES=ModuleParrot)");
    }

    /// <summary>
    /// Supprime les sections [ReflectorLogic] et [LinkToReflector] du fichier de configuration.
    /// Ces sections ne sont pas nécessaires en mode Perroquet local.
    /// </summary>
    private void RemoveReflectorSections(IniFile iniData)
    {
        iniData.RemoveSection("ReflectorLogic");
        iniData.RemoveSection("LinkToReflector");
        _logger.LogDebug("Sections [ReflectorLogic] et [LinkToReflector] supprimées pour le mode Perroquet");
    }

    /// <summary>
    /// Écrit le fichier de configuration de manière atomique (temp + rename).
    /// </summary>
    private async Task<Validation<Error, Unit>> WriteConfigurationAtomicallyAsync(
        IniFile iniData,
        string outputPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug("Répertoire créé: {Directory}", directory);
            }

            var tempPath = $"{outputPath}.tmp";

            var content = iniData.ToString();
            await File.WriteAllTextAsync(tempPath, content, cancellationToken);
            _logger.LogDebug("Fichier temporaire écrit: {TempPath} ({Length} bytes)", tempPath, content.Length);

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
                _logger.LogDebug("Ancien fichier supprimé: {OutputPath}", outputPath);
            }
            File.Move(tempPath, outputPath);

            _logger.LogInformation("Fichier Perroquet écrit de manière atomique: {OutputPath}", outputPath);
            return Success<Error, Unit>(unit);
        }
        catch (Exception ex)
        {
            var error = Error.New($"Erreur lors de l'écriture du fichier: {ex.Message}", ex);
            _logger.LogError(ex, "Erreur lors de l'écriture du fichier Perroquet: {OutputPath}", outputPath);
            return Validation<Error, Unit>.Fail(Seq1(error));
        }
    }

    /// <summary>
    /// Détermine le chemin du fichier template svxlink.conf.
    /// Utilise le chemin configuré ou cherche dans l'arborescence.
    /// </summary>
    private string GetTemplatePath()
    {
        if (!string.IsNullOrEmpty(_templatePath))
        {
            _logger.LogDebug("Chemin du template configuré: {TemplatePath}", _templatePath);
            return _templatePath;
        }

        var standardPath = Path.Combine(SvxLinkConfigDir, TemplateFileName);
        if (File.Exists(standardPath))
        {
            _logger.LogDebug("Template trouvé dans le répertoire standard: {TemplatePath}", standardPath);
            return standardPath;
        }

        // Fallback : chercher dans l'arborescence (environnement dev)
        var currentDirectory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        while (currentDirectory != null)
        {
            var templatePath = Path.Combine(currentDirectory.FullName, "svxlink-config", TemplateFileName);

            if (File.Exists(templatePath))
            {
                _logger.LogDebug("Template trouvé: {TemplatePath}", templatePath);
                return templatePath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        _logger.LogWarning("Template non trouvé, chemin par défaut: {TemplatePath}", standardPath);
        return standardPath;
    }
}
