using FluentAssertions;
using IniParser;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Infrastructure.SvxLink;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests d'intégration pour SvxLinkConfigurationService.
/// Valide la génération du fichier svxlink.conf avec le vrai template et ini-parser.
/// </summary>
public class SvxLinkConfigurationServiceTests : IDisposable
{
    private readonly SvxLinkConfigurationService _service;
    private readonly ILogger<SvxLinkConfigurationService> _logger;
    private readonly string _testOutputDirectory;
    private readonly List<string> _filesToCleanup;
    private readonly string _templatePath;

    public SvxLinkConfigurationServiceTests()
    {
        _logger = Substitute.For<ILogger<SvxLinkConfigurationService>>();
        
        // Trouver le chemin du template depuis le répertoire des tests
        _templatePath = FindTemplatePath();
        _service = new SvxLinkConfigurationService(_logger, _templatePath);
        
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
        var parser = new FileIniDataParser();
        var iniData = parser.ReadFile(outputPath);
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
        var parser = new FileIniDataParser();
        var iniData = parser.ReadFile(outputPath);

        iniData["GLOBAL"]["LOGICS"].Should().Be("SimplexLogic,ReflectorLogic");
        iniData["GLOBAL"]["CFG_DIR"].Should().Be("svxlink.d");
        iniData["GLOBAL"]["CARD_SAMPLE_RATE"].Should().Be("16000");
        iniData["GLOBAL"]["CARD_CHANNELS"].Should().Be("1");
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
        var parser = new FileIniDataParser();
        var iniData = parser.ReadFile(outputPath);

        iniData["ReflectorLogic"]["TYPE"].Should().Be("Reflector");
        iniData["ReflectorLogic"]["HOST"].Should().Be("ref.example.com");
        iniData["ReflectorLogic"]["PORT"].Should().Be("5300");
        iniData["ReflectorLogic"]["CALLSIGN"].Should().Be("F5TEST-L");
        iniData["ReflectorLogic"]["AUTH_KEY"].Should().Be("TestAuthKey123");
        iniData["ReflectorLogic"]["AUDIO_CODEC"].Should().Be("OPUS");
        iniData["ReflectorLogic"]["JITTER_BUFFER_DELAY"].Should().Be("0");
        iniData["ReflectorLogic"]["DEFAULT_LANG"].Should().Be("fr_FR");
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
        var parser = new FileIniDataParser();
        var iniData = parser.ReadFile(outputPath);

        iniData["SimplexLogic"]["TYPE"].Should().Be("Simplex");
        iniData["SimplexLogic"]["RX"].Should().Be("Rx1");
        iniData["SimplexLogic"]["TX"].Should().Be("Tx1");
        iniData["SimplexLogic"]["CALLSIGN"].Should().Be("F5TEST");
        iniData["SimplexLogic"]["MODULES"].Should().Be("ModuleHelp,ModuleParrot,ModuleTclVoiceMail");
        iniData["SimplexLogic"]["SHORT_IDENT_INTERVAL"].Should().Be("60");
        iniData["SimplexLogic"]["LONG_IDENT_INTERVAL"].Should().Be("60");
        iniData["SimplexLogic"]["EVENT_HANDLER"].Should().Be("/usr/share/svxlink/events.tcl");
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
        var parser = new FileIniDataParser();
        var iniData = parser.ReadFile(outputPath);

        // Les sections Rx1 et Tx1 doivent conserver leurs paramètres hardware du template
        iniData["Rx1"]["TYPE"].Should().Be("Local");
        iniData["Rx1"]["AUDIO_DEV"].Should().NotBeNullOrEmpty();
        
        iniData["Tx1"]["TYPE"].Should().Be("Local");
        iniData["Tx1"]["AUDIO_DEV"].Should().NotBeNullOrEmpty();
        iniData["Tx1"]["PTT_TYPE"].Should().NotBeNullOrEmpty();
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
        var parser = new FileIniDataParser();
        var iniData = parser.ReadFile(outputPath);

        iniData["SimplexLogic"]["REPORT_CTCSS"].Should().Be("136.5");
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
            AudioCodec: "OPUS",
            JitterBufferDelay: 0,
            SimplexCallsign: "F5TEST",
            Modules: "ModuleHelp,ModuleParrot,ModuleTclVoiceMail",
            ShortIdentInterval: 60,
            LongIdentInterval: 60,
            ReportCtcss: null,
            EventHandler: "/usr/share/svxlink/events.tcl",
            DefaultLang: "fr_FR",
            RgrSoundDelay: 0,
            SoundId: null,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 136.5m,
            TxCtcss: 136.5m
        );

        var result = SalonAggregate.Create(
            id: Guid.NewGuid(),
            name: "Salon Test",
            isDefault: false,
            isTemporized: false,
            configuration: configuration
        );

        return result.Match(
            Succ: salon => salon,
            Fail: errors => throw new Exception($"Impossible de créer le Salon de test: {errors}")
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
            AudioCodec: "OPUS",
            JitterBufferDelay: 0,
            SimplexCallsign: "F5TEST",
            Modules: "ModuleHelp,ModuleParrot,ModuleTclVoiceMail",
            ShortIdentInterval: 60,
            LongIdentInterval: 60,
            ReportCtcss: "136.5", // Valeur optionnelle présente
            EventHandler: "/usr/share/svxlink/events.tcl",
            DefaultLang: "fr_FR",
            RgrSoundDelay: 0,
            SoundId: null,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 136.5m,
            TxCtcss: 136.5m
        );

        var result = SalonAggregate.Create(
            id: Guid.NewGuid(),
            name: "Salon Test avec CTCSS",
            isDefault: false,
            isTemporized: false,
            configuration: configuration
        );

        return result.Match(
            Succ: salon => salon,
            Fail: errors => throw new Exception($"Impossible de créer le Salon de test: {errors}")
        );
    }

    private string GetTestOutputPath(string fileName)
    {
        var path = Path.Combine(_testOutputDirectory, fileName);
        _filesToCleanup.Add(path);
        return path;
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
