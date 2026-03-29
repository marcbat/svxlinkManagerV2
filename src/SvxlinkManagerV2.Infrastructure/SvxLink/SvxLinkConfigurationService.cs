using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Infrastructure.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Service de génération du fichier de configuration SVXLink (svxlink.conf).
/// Compatible avec SVXLink 19.09.2.
/// </summary>
public class SvxLinkConfigurationService : ISvxLinkConfigurationService
{
    private readonly ILogger<SvxLinkConfigurationService> _logger;
    private readonly string? _templatePath;
    private const string TemplateFileName = "svxlink.conf";
    private const string SvxLinkConfigDir = "/etc/svxlink";

    // Constructeur pour l'injection de dépendances (Wolverine/DI ne peut pas résoudre string? depuis le conteneur)
    public SvxLinkConfigurationService(ILogger<SvxLinkConfigurationService> logger)
        : this(logger, null) { }

    // Constructeur complet pour les tests (passage du chemin du template)
    public SvxLinkConfigurationService(
        ILogger<SvxLinkConfigurationService> logger,
        string? templatePath)
    {
        _logger = logger;
        _templatePath = templatePath;
    }

    /// <inheritdoc />
    public async Task<Validation<Error, Unit>> GenerateAsync(
        SalonAggregate salon,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Génération de la configuration SVXLink pour le Salon {SalonName} (ID: {SalonId})", 
                salon.Name, salon.Id);

            // 1. Localiser et charger le template
            var templatePath = GetTemplatePath();
            if (!File.Exists(templatePath))
            {
                var error = Error.New($"Le fichier template '{templatePath}' est introuvable");
                _logger.LogError("Template non trouvé: {TemplatePath}", templatePath);
                return Validation<Error, Unit>.Fail(Seq1(error));
            }

            // 2. Charger le template INI (support natif des commentaires avec notre parser)
            var iniData = await Task.Run(() => IniFile.Parse(templatePath), cancellationToken);

            // 3. Mettre à jour les sections avec les données du Salon
            UpdateGlobalSection(iniData, salon);
            UpdateLinkSection(iniData);
            UpdateReflectorLogicSection(iniData, salon);
            UpdateSimplexLogicSection(iniData, salon);
            UpdateReceiverSection(iniData, salon);
            UpdateTransmitterSection(iniData, salon);

            // 4. Écrire le fichier de manière atomique (temp + rename)
            var writeResult = await WriteConfigurationAtomicallyAsync(iniData, outputPath, cancellationToken);

            return writeResult.Match(
                Succ: _ =>
                {
                    _logger.LogInformation("Configuration SVXLink générée avec succès: {OutputPath}", outputPath);
                    return Success<Error, Unit>(unit);
                },
                Fail: errors =>
                {
                    _logger.LogError("Échec de l'écriture de la configuration: {Errors}", errors);
                    return Validation<Error, Unit>.Fail(errors);
                });
        }
        catch (Exception ex)
        {
            var error = Error.New($"Erreur lors de la génération de la configuration: {ex.Message}", ex);
            _logger.LogError(ex, "Exception lors de la génération de la configuration SVXLink");
            return Validation<Error, Unit>.Fail(Seq1(error));
        }
    }

    /// <inheritdoc />
    public async Task<Validation<Error, bool>> ValidateAsync(
        string configPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Validation du fichier de configuration: {ConfigPath}", configPath);

            if (!File.Exists(configPath))
            {
                var error = Error.New($"Le fichier de configuration '{configPath}' est introuvable");
                return Validation<Error, bool>.Fail(Seq1(error));
            }

            // Tenter de parser le fichier INI
            await Task.Run(() => IniFile.Parse(configPath), cancellationToken);

            _logger.LogInformation("Fichier de configuration valide: {ConfigPath}", configPath);
            return Success<Error, bool>(true);
        }
        catch (Exception ex)
        {
            var error = Error.New($"Le fichier de configuration est invalide: {ex.Message}", ex);
            _logger.LogError(ex, "Erreur de validation du fichier de configuration: {ConfigPath}", configPath);
            return Validation<Error, bool>.Fail(Seq1(error));
        }
    }

    /// <summary>
    /// Met à jour la section [GLOBAL] avec les valeurs du Salon.
    /// </summary>
    private void UpdateGlobalSection(IniFile iniData, SalonAggregate salon)
    {
        var config = salon.Configuration;
        
        // SimplexLogic doit toujours être présent pour que le RX/TX local fonctionne.
        // La valeur persistée en base peut être obsolète (migration depuis ancienne version).
        iniData["GLOBAL"]["LOGICS"] = "SimplexLogic,ReflectorLogic";
        iniData["GLOBAL"]["LINKS"] = "LinkToReflector";
        iniData["GLOBAL"]["CFG_DIR"] = config.CfgDir;
        iniData["GLOBAL"]["CARD_SAMPLE_RATE"] = config.CardSampleRate.ToString();
        iniData["GLOBAL"]["CARD_CHANNELS"] = config.CardChannels.ToString();

        _logger.LogDebug("Section [GLOBAL] mise à jour");
    }

    /// <summary>
    /// Met à jour la section [LinkToReflector] qui relie SimplexLogic et ReflectorLogic.
    /// Cette section est constante : SVXLink requiert ce pont pour router l'audio
    /// entre le hardware local (SimplexLogic) et le reflector (ReflectorLogic).
    /// </summary>
    private void UpdateLinkSection(IniFile iniData)
    {
        iniData["LinkToReflector"]["CONNECT_LOGICS"] = "SimplexLogic,ReflectorLogic";
        iniData["LinkToReflector"]["DEFAULT_ACTIVE"] = "1";
        iniData["LinkToReflector"]["TIMEOUT"] = "0";

        _logger.LogDebug("Section [LinkToReflector] mise à jour");
    }

    /// <summary>
    /// Met à jour la section [ReflectorLogic] avec les paramètres de connexion au Reflector.
    /// </summary>
    private void UpdateReflectorLogicSection(IniFile iniData, SalonAggregate salon)
    {
        var config = salon.Configuration;

        iniData["ReflectorLogic"]["TYPE"] = "Reflector";
        iniData["ReflectorLogic"]["HOST"] = config.Host;
        iniData["ReflectorLogic"]["PORT"] = config.Port.ToString();
        iniData["ReflectorLogic"]["CALLSIGN"] = config.Callsign;
        iniData["ReflectorLogic"]["AUTH_KEY"] = config.AuthKey;
        iniData["ReflectorLogic"]["AUDIO_CODEC"] = config.AudioCodec;
        iniData["ReflectorLogic"]["JITTER_BUFFER_DELAY"] = config.JitterBufferDelay.ToString();
        iniData["ReflectorLogic"]["DEFAULT_LANG"] = config.DefaultLang;

        _logger.LogDebug("Section [ReflectorLogic] mise à jour (Host: {Host}, Callsign: {Callsign})", 
            config.Host, config.Callsign);
    }

    /// <summary>
    /// Met à jour la section [SimplexLogic] avec les paramètres locaux.
    /// </summary>
    private void UpdateSimplexLogicSection(IniFile iniData, SalonAggregate salon)
    {
        var config = salon.Configuration;

        iniData["SimplexLogic"]["TYPE"] = "Simplex";
        iniData["SimplexLogic"]["RX"] = "Rx1";
        iniData["SimplexLogic"]["TX"] = "Tx1";
        iniData["SimplexLogic"]["MODULES"] = config.Modules;
        iniData["SimplexLogic"]["CALLSIGN"] = config.SimplexCallsign;
        iniData["SimplexLogic"]["SHORT_IDENT_INTERVAL"] = config.ShortIdentInterval.ToString();
        iniData["SimplexLogic"]["LONG_IDENT_INTERVAL"] = config.LongIdentInterval.ToString();
        iniData["SimplexLogic"]["IDENT_ONLY_AFTER_TX"] = "1";
        iniData["SimplexLogic"]["EXEC_CMD_ON_SQL_CLOSE"] = "1";
        // Chemin absolu requis — SVXLink résout le handler relatif au répertoire de travail, pas au SHARE_DIR
        // events.tcl est le point d'entrée principal qui source tous les handlers de events.d/ (dont SimplexLogic.tcl)
        iniData["SimplexLogic"]["EVENT_HANDLER"] = "/usr/share/svxlink/events.tcl";
        iniData["SimplexLogic"]["DEFAULT_LANG"] = config.DefaultLang;
        iniData["SimplexLogic"]["RGR_SOUND_DELAY"] = config.RgrSoundDelay.ToString();

        // REPORT_CTCSS est optionnel
        if (!string.IsNullOrEmpty(config.ReportCtcss))
        {
            iniData["SimplexLogic"]["REPORT_CTCSS"] = config.ReportCtcss;
        }

        _logger.LogDebug("Section [SimplexLogic] mise à jour (Callsign: {Callsign})", config.SimplexCallsign);
    }

    /// <summary>
    /// Met à jour la section [Rx1] avec les fréquences et CTCSS de réception.
    /// </summary>
    private void UpdateReceiverSection(IniFile iniData, SalonAggregate salon)
    {
        var config = salon.Configuration;

        // Garder les paramètres hardware existants du template (AUDIO_DEV, SQL_DET, GPIO, etc.)
        // Ne modifier que les fréquences/CTCSS qui viennent du Salon

        // Note: SVXLink ne gère PAS directement les fréquences dans svxlink.conf pour les receivers locaux.
        // Les fréquences sont configurées via le SA818 (hardware) avant le démarrage.
        // On log les valeurs pour traçabilité mais on ne les écrit pas dans svxlink.conf.

        _logger.LogDebug("Section [Rx1] - Fréquence RX configurée dans le hardware: {RxFrequency} MHz, CTCSS: {RxCtcss} Hz",
            config.RxFrequency, config.RxCtcss?.ToString() ?? "aucun");
        
        // Les paramètres Rx1 restent ceux du template (AUDIO_DEV, SQL_DET, GPIO, etc.)
    }

    /// <summary>
    /// Met à jour la section [Tx1] avec les fréquences et CTCSS de transmission.
    /// </summary>
    private void UpdateTransmitterSection(IniFile iniData, SalonAggregate salon)
    {
        var config = salon.Configuration;

        // Même logique que pour Rx1: les fréquences sont dans le hardware SA818, pas dans svxlink.conf
        _logger.LogDebug("Section [Tx1] - Fréquence TX configurée dans le hardware: {TxFrequency} MHz, CTCSS: {TxCtcss} Hz",
            config.TxFrequency, config.TxCtcss?.ToString() ?? "aucun");

        // Les paramètres Tx1 restent ceux du template (AUDIO_DEV, PTT_TYPE, GPIO, TIMEOUT, TX_DELAY, etc.)
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
            // S'assurer que le répertoire parent existe
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug("Répertoire créé: {Directory}", directory);
            }

            var tempPath = $"{outputPath}.tmp";

            // 1. Écrire dans un fichier temporaire
            var content = iniData.ToString();
            await File.WriteAllTextAsync(tempPath, content, cancellationToken);
            _logger.LogDebug("Fichier temporaire écrit: {TempPath} ({Length} bytes)", tempPath, content.Length);

            // 2. Remplacer l'ancien fichier (atomique sur UNIX, quasi-atomique sur Windows)
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
                _logger.LogDebug("Ancien fichier supprimé: {OutputPath}", outputPath);
            }
            File.Move(tempPath, outputPath);

            _logger.LogInformation("Fichier écrit de manière atomique: {OutputPath}", outputPath);
            return Success<Error, Unit>(unit);
        }
        catch (Exception ex)
        {
            var error = Error.New($"Erreur lors de l'écriture du fichier: {ex.Message}", ex);
            _logger.LogError(ex, "Erreur lors de l'écriture du fichier: {OutputPath}", outputPath);
            return Validation<Error, Unit>.Fail(Seq1(error));
        }
    }

    /// <summary>
    /// Détermine le chemin du fichier template svxlink.conf.
    /// Utilise le chemin configuré ou cherche dans l'arborescence.
    /// </summary>
    private string GetTemplatePath()
    {
        // Si un chemin explicite est configuré, l'utiliser
        if (!string.IsNullOrEmpty(_templatePath))
        {
            _logger.LogDebug("Chemin du template configuré: {TemplatePath}", _templatePath);
            return _templatePath;
        }

        // Chercher dans le répertoire standard SVXLink (/etc/svxlink/)
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

        // Si non trouvé, retourner le chemin standard qui provoquera une erreur explicite
        _logger.LogWarning("Template non trouvé, chemin par défaut: {TemplatePath}", standardPath);
        return standardPath;
    }
}
