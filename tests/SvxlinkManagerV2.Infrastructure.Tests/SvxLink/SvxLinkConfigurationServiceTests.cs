using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Infrastructure.Common;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using SvxlinkManagerV2.Infrastructure.SvxLink.Strategies;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests d'intégration pour SvxLinkConfigurationService.
/// Valide la génération du fichier svxlink.conf avec le vrai template et ini-parser.
/// </summary>
public class SvxLinkConfigurationServiceTests : IDisposable
{
    private readonly SvxLinkConfigurationService _service;
    private readonly ILogger<SvxLinkConfigurationService> _logger;
    private readonly ISvxLinkStrategyResolver _strategyResolver;
    private readonly string _testOutputDirectory;
    private readonly List<string> _filesToCleanup;
    private readonly string _templatePath;

    public SvxLinkConfigurationServiceTests()
    {
        _logger = Substitute.For<ILogger<SvxLinkConfigurationService>>();
        
        // Trouver le chemin du template depuis le répertoire des tests
        _templatePath = FindTemplatePath();

        // Créer un resolver avec les vraies stratégies
        _strategyResolver = new SvxLinkStrategyResolver(new ISvxLinkVersionStrategy[]
        {
            new SvxLinkLegacyStrategy(),
            new SvxLinkModernStrategy()
        });

        _service = new SvxLinkConfigurationService(_logger, _strategyResolver, _templatePath);
        
        // Créer un répertoire temporaire pour les tests
        _testOutputDirectory = Path.Combine(Path.GetTempPath(), $"svxlink-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testOutputDirectory);
        _filesToCleanup = new List<string>();
    }

    private static string FindTemplatePath()
    {
        // Remonter depuis le répertoire de tests pour trouver la racine du projet
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        
        while (currentDir != null)
        {
            var templatePath = Path.Combine(currentDir.FullName, "svxlink-config", "svxlink.conf");
            if (File.Exists(templatePath))
            {
                return templatePath;
            }
            currentDir = currentDir.Parent;
        }
        
        throw new FileNotFoundException("Template svxlink.conf non trouvé dans l'arborescence");
    }

    [Fact]
    public async Task GenerateAsync_WithValidSalon_ShouldCreateValidConfigurationFile()
    {
        // Arrange
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink.conf");

        // Act
        var result = await _service.GenerateAsync(salon, outputPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();

        // Vérifier que le fichier est un INI valide
        var iniData = IniFile.Parse(outputPath);
        iniData.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_ShouldUpdateGlobalSection()
    {
        // Arrange
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_global.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);

        iniData["GLOBAL"]["LOGICS"].Should().Be("SimplexLogic,ReflectorLogic");
        iniData["GLOBAL"]["LINKS"].Should().Be("LinkToReflector");
        iniData["GLOBAL"]["CFG_DIR"].Should().Be("svxlink.d");
        iniData["GLOBAL"]["CARD_SAMPLE_RATE"].Should().Be("16000");
        iniData["GLOBAL"]["CARD_CHANNELS"].Should().Be("1");
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeLinksInGlobalSection()
    {
        // Arrange
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_links_global.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);

        iniData["GLOBAL"]["LINKS"].Should().Be("LinkToReflector");
    }

    [Fact]
    public async Task GenerateAsync_ShouldCreateLinkToReflectorSection()
    {
        // Arrange
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_linktoreflector.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);

        iniData["LinkToReflector"]["CONNECT_LOGICS"].Should().Be("SimplexLogic,ReflectorLogic");
        iniData["LinkToReflector"]["DEFAULT_ACTIVE"].Should().Be("1");
        iniData["LinkToReflector"]["TIMEOUT"].Should().Be("0");
    }

    [Fact]
    public async Task GenerateAsync_ShouldUpdateReflectorLogicSection()
    {
        // Arrange
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_reflector.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);

        iniData["ReflectorLogic"]["TYPE"].Should().Be("Reflector"); // V2 protocol uses simple "Reflector" type
        iniData["ReflectorLogic"]["HOST"].Should().Be("ref.example.com");
        iniData["ReflectorLogic"]["PORT"].Should().Be("5300");
        iniData["ReflectorLogic"]["CALLSIGN"].Should().Be("F5TEST-L");
        iniData["ReflectorLogic"]["AUTH_KEY"].Should().Be("TestAuthKey123");
        iniData["ReflectorLogic"]["AUDIO_CODEC"].Should().Be("OPUS");
        iniData["ReflectorLogic"]["JITTER_BUFFER_DELAY"].Should().Be("0");
        iniData["ReflectorLogic"]["DEFAULT_LANG"].Should().Be("fr_FR");
    }

    [Fact]
    public async Task GenerateAsync_ShouldUpdateReflectorLogicSectionForV3Protocol()
    {
        // Arrange
        var salon = CreateTestSalonV3();
        var outputPath = GetTestOutputPath("svxlink_reflector_v3.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);

        iniData["ReflectorLogic"]["TYPE"].Should().Be("Reflector"); // V3 protocol uses "Reflector" type (same as V2, but different config)
        iniData["ReflectorLogic"]["HOSTS"].Should().Be("ref.example.com:5300");
        iniData["ReflectorLogic"]["CALLSIGN"].Should().Be("F5TEST-L");
        iniData["ReflectorLogic"]["AUDIO_CODEC"].Should().Be("OPUS");
        iniData["ReflectorLogic"]["JITTER_BUFFER_DELAY"].Should().Be("0");
        iniData["ReflectorLogic"]["DEFAULT_LANG"].Should().Be("fr_FR");
        iniData["ReflectorLogic"]["CERT_PKI_DIR"].Should().Be("/var/lib/svxlink/pki");
        iniData["ReflectorLogic"]["CERT_EMAIL"].Should().Be("test@example.com");
        
        // V3 should not have V2-specific keys
        iniData["ReflectorLogic"].ContainsKey("AUTH_KEY").Should().BeFalse();
        iniData["ReflectorLogic"].ContainsKey("HOST").Should().BeFalse();
        iniData["ReflectorLogic"].ContainsKey("PORT").Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_V3_ShouldSetTalkGroupParameters()
    {
        // Arrange
        var salon = CreateTestSalonV3();
        var outputPath = GetTestOutputPath("svxlink_reflector_v3_tg.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);
        iniData["ReflectorLogic"]["DEFAULT_TG"].Should().Be("0");
        iniData["ReflectorLogic"]["TG_SELECT_TIMEOUT"].Should().Be("30");
        iniData["ReflectorLogic"]["MUTE_FIRST_TX_LOC"].Should().Be("1");
        iniData["ReflectorLogic"]["MUTE_FIRST_TX_REM"].Should().Be("0");
        iniData["ReflectorLogic"]["TMP_MONITOR_TIMEOUT"].Should().Be("3600");
        iniData["ReflectorLogic"]["QSY_PENDING_TIMEOUT"].Should().Be("-1");
        iniData["ReflectorLogic"].ContainsKey("MONITOR_TGS").Should().BeFalse();
        iniData["ReflectorLogic"].ContainsKey("TG_SELECT_INHIBIT_TIMEOUT").Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_V2_ShouldNotSetTalkGroupParameters()
    {
        // Arrange
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_reflector_v2_tg.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);
        iniData["ReflectorLogic"].ContainsKey("DEFAULT_TG").Should().BeFalse();
        iniData["ReflectorLogic"].ContainsKey("MONITOR_TGS").Should().BeFalse();
        iniData["ReflectorLogic"].ContainsKey("TG_SELECT_TIMEOUT").Should().BeFalse();
        iniData["ReflectorLogic"].ContainsKey("TG_SELECT_INHIBIT_TIMEOUT").Should().BeFalse();
        iniData["ReflectorLogic"].ContainsKey("MUTE_FIRST_TX_LOC").Should().BeFalse();
        iniData["ReflectorLogic"].ContainsKey("MUTE_FIRST_TX_REM").Should().BeFalse();
        iniData["ReflectorLogic"].ContainsKey("TMP_MONITOR_TIMEOUT").Should().BeFalse();
        iniData["ReflectorLogic"].ContainsKey("QSY_PENDING_TIMEOUT").Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_V3_WithMonitorTgs_ShouldSetMonitorTgs()
    {
        // Arrange
        var salon = CreateTestSalonV3WithTalkGroups("91,208,226+", 15);
        var outputPath = GetTestOutputPath("svxlink_reflector_v3_monitor_tgs.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);
        iniData["ReflectorLogic"]["MONITOR_TGS"].Should().Be("91,208,226+");
        iniData["ReflectorLogic"]["TG_SELECT_INHIBIT_TIMEOUT"].Should().Be("15");
    }

    [Fact]
    public async Task GenerateAsync_ShouldUpdateSimplexLogicSection()
    {
        // Arrange
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_simplex.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);

        iniData["SimplexLogic"]["TYPE"].Should().Be("Simplex");
        iniData["SimplexLogic"]["RX"].Should().Be("Rx1");
        iniData["SimplexLogic"]["TX"].Should().Be("Tx1");
        iniData["SimplexLogic"]["CALLSIGN"].Should().Be("F5TEST");
        iniData["SimplexLogic"]["MODULES"].Should().Be("ModuleHelp,ModuleParrot,ModuleTclVoiceMail");
        iniData["SimplexLogic"]["SHORT_IDENT_INTERVAL"].Should().Be("60");
        iniData["SimplexLogic"]["LONG_IDENT_INTERVAL"].Should().Be("60");
        iniData["SimplexLogic"]["EVENT_HANDLER"].Should().Be("/opt/svxlink-legacy/share/svxlink/events.tcl");
        iniData["SimplexLogic"]["DEFAULT_LANG"].Should().Be("fr_FR");
        iniData["SimplexLogic"]["RGR_SOUND_DELAY"].Should().Be("0");
    }

    [Fact]
    public async Task GenerateAsync_ShouldPreserveReceiverAndTransmitterSections()
    {
        // Arrange
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_hardware.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);

        // Les sections Rx1 et Tx1 doivent conserver leurs paramètres hardware du template
        iniData["Rx1"]["TYPE"].Should().Be("Local");
        iniData["Rx1"]["AUDIO_DEV"].Should().NotBeNullOrEmpty();
        
        iniData["Tx1"]["TYPE"].Should().Be("Local");
        iniData["Tx1"]["AUDIO_DEV"].Should().NotBeNullOrEmpty();
        iniData["Tx1"]["PTT_TYPE"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WithNullCtcss_ShouldGenerateValidConfig()
    {
        // Arrange - Salon avec CTCSS null (pas de sous-ton)
        var salon = CreateTestSalonWithNullCtcss();
        var outputPath = GetTestOutputPath("svxlink_null_ctcss.conf");

        // Act
        var result = await _service.GenerateAsync(salon, outputPath);

        // Assert - La génération doit réussir même sans CTCSS
        result.IsSuccess.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();

        // Le fichier doit être un INI valide
        var iniData = IniFile.Parse(outputPath);
        iniData.Should().NotBeNull();

        // Les paramètres reflector doivent être présents
        iniData["ReflectorLogic"]["HOST"].Should().Be("ref.example.com");
        iniData["ReflectorLogic"]["PORT"].Should().Be("5300");
        iniData["ReflectorLogic"]["CALLSIGN"].Should().Be("F5TEST-L");
    }

    [Fact]
    public async Task GenerateAsync_WithOptionalReportCtcss_ShouldIncludeInConfig()
    {
        // Arrange
        var salon = CreateTestSalonWithReportCtcss();
        var outputPath = GetTestOutputPath("svxlink_ctcss.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);

        iniData["SimplexLogic"]["REPORT_CTCSS"].Should().Be("136.5");
    }

    [Fact]
    public async Task GenerateAsync_ShouldNotSetAnyAnnounceParameters()
    {
        // Arrange — l'annonce one-shot est gérée par Logic.tcl (proc startup {}),
        // aucun paramètre d'annonce n'est écrit dans svxlink.conf (non supporté SVXLink 19.09.2)
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_no_announce.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert — aucun paramètre ANNOUNCE_* ne doit apparaître dans le fichier généré
        var iniData = IniFile.Parse(outputPath);

        iniData["SimplexLogic"].ContainsKey("STARTUP_ANNOUNCEMENTS").Should().BeFalse();
        iniData["SimplexLogic"].ContainsKey("SHORT_ANNOUNCE_FILE").Should().BeFalse();
        iniData["SimplexLogic"].ContainsKey("LONG_ANNOUNCE_FILE").Should().BeFalse();
        iniData["SimplexLogic"].ContainsKey("SHORT_ANNOUNCE_ENABLE").Should().BeFalse();
        iniData["SimplexLogic"].ContainsKey("LONG_ANNOUNCE_ENABLE").Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WithValidFile_ShouldReturnSuccess()
    {
        // Arrange
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_valid.conf");
        await _service.GenerateAsync(salon, outputPath);

        // Act
        var result = await _service.ValidateAsync(outputPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            Succ: isValid => isValid.Should().BeTrue(),
            Fail: _ => throw new Exception("Validation ne devrait pas échouer")
        );
    }

    [Fact]
    public async Task ValidateAsync_WithNonExistentFile_ShouldReturnFailure()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testOutputDirectory, "nonexistent.conf");

        // Act
        var result = await _service.ValidateAsync(nonExistentPath);

        // Assert
        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_ShouldWriteAtomically()
    {
        // Arrange
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_atomic.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        
        // Vérifier qu'il n'y a pas de fichier .tmp restant
        var tempPath = $"{outputPath}.tmp";
        File.Exists(tempPath).Should().BeFalse();
    }

    // Helper methods

    private SalonAggregate CreateTestSalon()
    {
        var configuration = new SvxLinkConfiguration(
            Id: Guid.NewGuid(),
            Logics: "SimplexLogic,ReflectorLogic",
            CfgDir: "svxlink.d",
            CardSampleRate: 16000,
            CardChannels: 1,
            Host: "ref.example.com",
            Port: 5300,
            Callsign: "F5TEST-L",
            AuthKey: "TestAuthKey123",
            JitterBufferDelay: 0,
            ReflectorProtocol: ReflectorProtocol.V2,
            CertEmail: null,
            SimplexCallsign: "F5TEST",
            Modules: "ModuleHelp,ModuleParrot,ModuleTclVoiceMail",
            ShortIdentInterval: 60,
            LongIdentInterval: 60,
            ReportCtcss: null,
            DefaultLang: "fr_FR",
            RgrSoundDelay: 0,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 136.5m,
            TxCtcss: 136.5m
        );

        var result = SalonAggregate.Create(
            id: Guid.NewGuid(),
            name: "Salon Test",
            isDefault: false,
            configuration: configuration
        );

        return result.Match(
            Succ: salon => salon,
            Fail: errors => throw new Exception($"Impossible de créer le Salon de test: {errors}")
        );
    }

    private SalonAggregate CreateTestSalonV3()
    {
        var configuration = new SvxLinkConfiguration(
            Id: Guid.NewGuid(),
            Logics: "SimplexLogic,ReflectorLogic",
            CfgDir: "svxlink.d",
            CardSampleRate: 16000,
            CardChannels: 1,
            Host: "ref.example.com",
            Port: 5300,
            Callsign: "F5TEST-L",
            AuthKey: null, // V3 uses certificates, not AUTH_KEY
            JitterBufferDelay: 0,
            ReflectorProtocol: ReflectorProtocol.V3,
            CertEmail: "test@example.com",
            SimplexCallsign: "F5TEST",
            Modules: "ModuleHelp,ModuleParrot,ModuleTclVoiceMail",
            ShortIdentInterval: 60,
            LongIdentInterval: 60,
            ReportCtcss: null,
            DefaultLang: "fr_FR",
            RgrSoundDelay: 0,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 136.5m,
            TxCtcss: 136.5m
        );

        var result = SalonAggregate.Create(
            id: Guid.NewGuid(),
            name: "Salon Test V3",
            isDefault: false,
            configuration: configuration
        );

        return result.Match(
            Succ: salon => salon,
            Fail: errors => throw new Exception($"Impossible de créer le Salon de test V3: {errors}")
        );
    }

    private SalonAggregate CreateTestSalonV3WithTalkGroups(string monitorTgs, int? tgSelectInhibitTimeout)
    {
        var baseConfig = CreateTestSalonV3().Configuration;
        var configuration = baseConfig with
        {
            MonitorTgs = monitorTgs,
            TgSelectInhibitTimeout = tgSelectInhibitTimeout
        };

        var result = SalonAggregate.Create(
            id: Guid.NewGuid(),
            name: "Salon Test V3 TG",
            isDefault: false,
            configuration: configuration
        );

        return result.Match(
            Succ: salon => salon,
            Fail: errors => throw new Exception($"Impossible de créer le Salon de test V3 TG: {errors}")
        );
    }

    private SalonAggregate CreateTestSalonWithReportCtcss()
    {
        var configuration = new SvxLinkConfiguration(
            Id: Guid.NewGuid(),
            Logics: "SimplexLogic,ReflectorLogic",
            CfgDir: "svxlink.d",
            CardSampleRate: 16000,
            CardChannels: 1,
            Host: "ref.example.com",
            Port: 5300,
            Callsign: "F5TEST-L",
            AuthKey: "TestAuthKey123",
            JitterBufferDelay: 0,
            ReflectorProtocol: ReflectorProtocol.V2,
            CertEmail: null,
            SimplexCallsign: "F5TEST",
            Modules: "ModuleHelp,ModuleParrot,ModuleTclVoiceMail",
            ShortIdentInterval: 60,
            LongIdentInterval: 60,
            ReportCtcss: "136.5", // Valeur optionnelle présente
            DefaultLang: "fr_FR",
            RgrSoundDelay: 0,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 136.5m,
            TxCtcss: 136.5m
        );

        var result = SalonAggregate.Create(
            id: Guid.NewGuid(),
            name: "Salon Test avec CTCSS",
            isDefault: false,
            configuration: configuration
        );

        return result.Match(
            Succ: salon => salon,
            Fail: errors => throw new Exception($"Impossible de créer le Salon de test: {errors}")
        );
    }

    private SalonAggregate CreateTestSalonWithNullCtcss()
    {
        var configuration = new SvxLinkConfiguration(
            Id: Guid.NewGuid(),
            Logics: "SimplexLogic,ReflectorLogic",
            CfgDir: "svxlink.d",
            CardSampleRate: 16000,
            CardChannels: 1,
            Host: "ref.example.com",
            Port: 5300,
            Callsign: "F5TEST-L",
            AuthKey: "TestAuthKey123",
            JitterBufferDelay: 0,
            ReflectorProtocol: ReflectorProtocol.V2,
            CertEmail: null,
            SimplexCallsign: "F5TEST",
            Modules: "ModuleHelp",
            ShortIdentInterval: 60,
            LongIdentInterval: 60,
            ReportCtcss: null,
            DefaultLang: "fr_FR",
            RgrSoundDelay: 0,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: null,   // Pas de sous-ton RX
            TxCtcss: null    // Pas de sous-ton TX
        );

        var result = SalonAggregate.Create(
            id: Guid.NewGuid(),
            name: "Salon Sans CTCSS",
            isDefault: false,
            configuration: configuration
        );

        return result.Match(
            Succ: salon => salon,
            Fail: errors => throw new Exception($"Impossible de créer le Salon de test sans CTCSS: {errors}")
        );
    }

    private string GetTestOutputPath(string fileName)
    {
        var path = Path.Combine(_testOutputDirectory, fileName);
        _filesToCleanup.Add(path);
        return path;
    }

    [Fact]
    public async Task GenerateStandaloneAsync_ShouldCreateValidConfigurationFile()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_standalone.conf");

        // Act
        var result = await _service.GenerateStandaloneAsync(145.550m, 145.550m, outputPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();

        var iniData = IniFile.Parse(outputPath);
        iniData.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateStandaloneAsync_ShouldSetLogicsToSimplexOnly()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_standalone_global.conf");

        // Act
        var result = await _service.GenerateStandaloneAsync(144.800m, 144.200m, outputPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var iniData = IniFile.Parse(outputPath);
        iniData["GLOBAL"]["LOGICS"].Should().Be("SimplexLogic");
        iniData["GLOBAL"]["LOGICS"].Should().NotContain("ReflectorLogic");
    }

    [Fact]
    public async Task GenerateStandaloneAsync_ShouldNotHaveLinksKey()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_standalone_links.conf");

        // Act
        var result = await _service.GenerateStandaloneAsync(145.550m, 145.550m, outputPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var iniData = IniFile.Parse(outputPath);
        // La clé LINKS ne doit pas être présente (mode simplex sans réflecteur)
        iniData["GLOBAL"].ContainsKey("LINKS").Should().BeFalse();
    }

    [Fact]
    public async Task GenerateStandaloneAsync_ShouldSetSimplexLogicDefaults()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_standalone_simplex.conf");

        // Act
        var result = await _service.GenerateStandaloneAsync(144.800m, 144.200m, outputPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var iniData = IniFile.Parse(outputPath);
        iniData["SimplexLogic"]["TYPE"].Should().Be("Simplex");
        iniData["SimplexLogic"]["RX"].Should().Be("Rx1");
        iniData["SimplexLogic"]["TX"].Should().Be("Tx1");
        iniData["SimplexLogic"]["CALLSIGN"].Should().Be("F0DTMF");
        iniData["SimplexLogic"]["DEFAULT_LANG"].Should().Be("fr_FR");
    }

    [Fact]
    public async Task GenerateStandaloneAsync_ShouldLeaveNoTempFile()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_standalone_atomic.conf");

        // Act
        await _service.GenerateStandaloneAsync(145.550m, 145.550m, outputPath);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var tempPath = $"{outputPath}.tmp";
        File.Exists(tempPath).Should().BeFalse();
    }

    public void Dispose()
    {
        // Nettoyer les fichiers de test
        foreach (var file in _filesToCleanup.Where(File.Exists))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignorer les erreurs de nettoyage
            }
        }

        // Nettoyer le répertoire
        if (Directory.Exists(_testOutputDirectory))
        {
            try
            {
                Directory.Delete(_testOutputDirectory, true);
            }
            catch
            {
                // Ignorer les erreurs de nettoyage
            }
        }
    }
}
