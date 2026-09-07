using FluentAssertions;
using LanguageExt;
using Unit = LanguageExt.Unit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Entities;
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
    private readonly IGeneralConfigurationRepository _generalConfigurationRepository;
    private readonly INodeInformationWriter _nodeInformationWriter;
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

        // Configuration générale absente par défaut : aucune variable CERT_SUBJ_* n'est
        // alors écrite, ce qui est l'état d'un nœud qui n'a pas renseigné son identité.
        _generalConfigurationRepository = Substitute.For<IGeneralConfigurationRepository>();
        _generalConfigurationRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);

        // L'écriture du document du nœud est éprouvée à part : ici seule compte la
        // déclaration de NODE_INFO_FILE dans la configuration générée.
        _nodeInformationWriter = Substitute.For<INodeInformationWriter>();
        _nodeInformationWriter.WriteAsync(Arg.Any<SalonAggregate>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LanguageExt.Common.Error, Unit>>(Unit.Default));

        _service = new SvxLinkConfigurationService(
            _logger, _strategyResolver, _generalConfigurationRepository,
            _nodeInformationWriter, _templatePath);
        
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
    public async Task GenerateAsync_WithV2Salon_ShouldCreateLinkToReflectorSectionWithoutCommandPrefix()
    {
        // Arrange — SVXLink 19.09.2 ne connaît pas les talkgroups : aucun préfixe à déclarer.
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

    #region Redondance de serveurs

    /// <summary>
    /// Un salon à serveur unique doit produire exactement la configuration d'avant : c'est
    /// la garantie qu'aucun salon existant ne change de comportement.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_WithASingleServer_ShouldKeepTheHistoricalHosts()
    {
        var salon = CreateTestSalonV3();
        var outputPath = GetTestOutputPath("svxlink_hosts_single.conf");

        await _service.GenerateAsync(salon, outputPath);

        var section = IniFile.Parse(outputPath)["ReflectorLogic"];
        section["HOSTS"].Should().Be($"{salon.Configuration.Host}:{salon.Configuration.Port}");
        section.ContainsKey("DNS_DOMAIN").Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WithAdditionalServers_ShouldListThemInOrder()
    {
        var salon = CreateTestSalonV3WithServers(additionalHosts: "secours.example.org:5301, tertiaire.example.org");
        var outputPath = GetTestOutputPath("svxlink_hosts_multiple.conf");

        await _service.GenerateAsync(salon, outputPath);

        var hosts = IniFile.Parse(outputPath)["ReflectorLogic"]["HOSTS"];
        hosts.Should().Be($"{salon.Configuration.Host}:{salon.Configuration.Port}"
                          + ",secours.example.org:5301,tertiaire.example.org:5300");
    }

    [Fact]
    public async Task GenerateAsync_ShouldDeclareTheDefaultPortForEntriesWithoutOne()
    {
        var salon = CreateTestSalonV3WithServers(additionalHosts: "secours.example.org");
        var outputPath = GetTestOutputPath("svxlink_hosts_port.conf");

        await _service.GenerateAsync(salon, outputPath);

        IniFile.Parse(outputPath)["ReflectorLogic"]["HOST_PORT"]
            .Should().Be(salon.Configuration.Port.ToString());
    }

    [Fact]
    public async Task GenerateAsync_WithADnsDomain_ShouldDeclareIt()
    {
        var salon = CreateTestSalonV3WithServers(dnsDomain: "exemple.org");
        var outputPath = GetTestOutputPath("svxlink_hosts_dns.conf");

        await _service.GenerateAsync(salon, outputPath);

        IniFile.Parse(outputPath)["ReflectorLogic"]["DNS_DOMAIN"].Should().Be("exemple.org");
    }

    /// <summary>
    /// SVXLink 19.09.2 ne connaît que HOST et PORT : lui écrire une liste le laisserait sans
    /// serveur du tout.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_WithAV2Salon_ShouldNotDeclareAnyRedundancy()
    {
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_hosts_v2.conf");

        await _service.GenerateAsync(salon, outputPath);

        var section = IniFile.Parse(outputPath)["ReflectorLogic"];
        section.ContainsKey("HOSTS").Should().BeFalse();
        section.ContainsKey("HOST_PORT").Should().BeFalse();
        section.ContainsKey("DNS_DOMAIN").Should().BeFalse();
        section["HOST"].Should().Be(salon.Configuration.Host);
    }

    private SalonAggregate CreateTestSalonV3WithServers(
        string? additionalHosts = null,
        string? dnsDomain = null)
    {
        var config = CreateTestSalonV3().Configuration with
        {
            AdditionalHosts = additionalHosts,
            DnsDomain = dnsDomain
        };

        return SalonAggregate.Create(Guid.NewGuid(), "Salon V3", false, config).Match(
            Succ: aggregate => aggregate,
            Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));
    }

    #endregion

    #region Paramètres V3 supplémentaires

    /// <summary>
    /// La configuration générée dit ce que l'opérateur a choisi, pas ce que SVXLink aurait
    /// fait de toute façon : une valeur laissée au défaut n'est pas écrite.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_WithDefaultValues_ShouldNotWriteTheOptionalV3Settings()
    {
        var salon = CreateTestSalonV3();
        var outputPath = GetTestOutputPath("svxlink_v3_defaults.conf");

        await _service.GenerateAsync(salon, outputPath);

        var section = IniFile.Parse(outputPath)["ReflectorLogic"];
        section.ContainsKey("UDP_HEARTBEAT_INTERVAL").Should().BeFalse();
        section.ContainsKey("ANNOUNCE_REMOTE_MIN_INTERVAL").Should().BeFalse();
        section.ContainsKey("VERBOSE").Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WithATunedHeartbeat_ShouldWriteIt()
    {
        // Le remède documenté aux déconnexions par expiration de présence UDP.
        var salon = CreateTestSalonV3WithSettings(udpHeartbeatInterval: 5);
        var outputPath = GetTestOutputPath("svxlink_v3_heartbeat.conf");

        await _service.GenerateAsync(salon, outputPath);

        IniFile.Parse(outputPath)["ReflectorLogic"]["UDP_HEARTBEAT_INTERVAL"].Should().Be("5");
    }

    [Fact]
    public async Task GenerateAsync_WithAnAnnouncementInterval_ShouldWriteIt()
    {
        var salon = CreateTestSalonV3WithSettings(announceRemoteMinInterval: 120);
        var outputPath = GetTestOutputPath("svxlink_v3_announce.conf");

        await _service.GenerateAsync(salon, outputPath);

        IniFile.Parse(outputPath)["ReflectorLogic"]["ANNOUNCE_REMOTE_MIN_INTERVAL"].Should().Be("120");
    }

    [Fact]
    public async Task GenerateAsync_WithVerboseDisabled_ShouldWriteZero()
    {
        var salon = CreateTestSalonV3WithSettings(verbose: false);
        var outputPath = GetTestOutputPath("svxlink_v3_verbose.conf");

        await _service.GenerateAsync(salon, outputPath);

        IniFile.Parse(outputPath)["ReflectorLogic"]["VERBOSE"].Should().Be("0");
    }

    [Fact]
    public async Task GenerateAsync_WithAV2Salon_ShouldNotWriteTheV3Settings()
    {
        var salon = CreateTestSalon();
        var outputPath = GetTestOutputPath("svxlink_v2_no_v3_settings.conf");

        await _service.GenerateAsync(salon, outputPath);

        var section = IniFile.Parse(outputPath)["ReflectorLogic"];
        section.ContainsKey("UDP_HEARTBEAT_INTERVAL").Should().BeFalse();
        section.ContainsKey("ANNOUNCE_REMOTE_MIN_INTERVAL").Should().BeFalse();
        section.ContainsKey("VERBOSE").Should().BeFalse();
        section.ContainsKey("CERT_SUBJ_GN").Should().BeFalse();
    }

    #endregion

    #region Identité du certificat

    [Fact]
    public async Task GenerateAsync_WithACertificateSubject_ShouldWriteEveryFilledField()
    {
        GivenCertificateSubject(new CertificateSubject(
            GivenName: "Marc", Surname: "Battaglia", Organization: "Radio-club",
            Locality: "Genève", Country: "CH"));

        var outputPath = GetTestOutputPath("svxlink_v3_subject.conf");
        await _service.GenerateAsync(CreateTestSalonV3(), outputPath);

        var section = IniFile.Parse(outputPath)["ReflectorLogic"];
        section["CERT_SUBJ_GN"].Should().Be("Marc");
        section["CERT_SUBJ_SN"].Should().Be("Battaglia");
        section["CERT_SUBJ_O"].Should().Be("Radio-club");
        section["CERT_SUBJ_L"].Should().Be("Genève");
        section["CERT_SUBJ_C"].Should().Be("CH");
    }

    [Fact]
    public async Task GenerateAsync_ShouldOmitTheEmptyCertificateFields()
    {
        // Un sujet partiel est légitime : SVXLink construit alors un sujet réduit.
        GivenCertificateSubject(new CertificateSubject(GivenName: "Marc"));

        var outputPath = GetTestOutputPath("svxlink_v3_subject_partial.conf");
        await _service.GenerateAsync(CreateTestSalonV3(), outputPath);

        var section = IniFile.Parse(outputPath)["ReflectorLogic"];
        section["CERT_SUBJ_GN"].Should().Be("Marc");
        section.ContainsKey("CERT_SUBJ_SN").Should().BeFalse();
        section.ContainsKey("CERT_SUBJ_C").Should().BeFalse();
    }

    /// <summary>
    /// Un champ effacé par l'opérateur doit disparaître du fichier : sans ce nettoyage,
    /// l'ancienne valeur survivrait dans le template et continuerait d'être signée.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_WhenAFieldIsCleared_ShouldRemoveItFromTheConfiguration()
    {
        GivenCertificateSubject(new CertificateSubject(GivenName: "Marc", Surname: "Battaglia"));
        var outputPath = GetTestOutputPath("svxlink_v3_subject_cleared.conf");
        await _service.GenerateAsync(CreateTestSalonV3(), outputPath);

        GivenCertificateSubject(new CertificateSubject(GivenName: "Marc"));
        await _service.GenerateAsync(CreateTestSalonV3(), outputPath);

        var section = IniFile.Parse(outputPath)["ReflectorLogic"];
        section["CERT_SUBJ_GN"].Should().Be("Marc");
        section.ContainsKey("CERT_SUBJ_SN").Should().BeFalse();
    }

    private void GivenCertificateSubject(CertificateSubject subject)
    {
        var configuration = GeneralConfigurationAggregate.Create(certificateSubject: subject).Match(
            Succ: aggregate => aggregate,
            Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));

        _generalConfigurationRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(configuration);
    }

    private SalonAggregate CreateTestSalonV3WithSettings(
        int udpHeartbeatInterval = 15,
        int announceRemoteMinInterval = 0,
        bool verbose = true)
    {
        var config = CreateTestSalonV3().Configuration with
        {
            UdpHeartbeatInterval = udpHeartbeatInterval,
            AnnounceRemoteMinInterval = announceRemoteMinInterval,
            Verbose = verbose
        };

        return SalonAggregate.Create(Guid.NewGuid(), "Salon V3", false, config).Match(
            Succ: aggregate => aggregate,
            Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));
    }

    #endregion

    [Theory]
    [InlineData(false, "/opt/svxlink-legacy/share/svxlink/events.tcl")]
    [InlineData(true, "/opt/svxlink-modern/share/svxlink/events.tcl")]
    public async Task GenerateAsync_ShouldPointReflectorLogicAtTheRootEventHandler(bool v3, string expected)
    {
        // Pointer events.d/local/Logic.tcl directement prive l'interpréteur de ReflectorLogic
        // de son propre namespace : SVXLink appelle ReflectorLogic::report_tg_status ou
        // ::tg_selected et échoue sur « invalid command name ». C'est events.tcl qui charge
        // les gestionnaires standards puis nos surcharges locales.
        var salon = v3 ? CreateTestSalonV3() : CreateTestSalon();
        var outputPath = GetTestOutputPath($"svxlink_reflector_eventhandler_{(v3 ? "v3" : "v2")}.conf");

        await _service.GenerateAsync(salon, outputPath);

        var iniData = IniFile.Parse(outputPath);

        iniData["ReflectorLogic"]["EVENT_HANDLER"].Should().Be(expected);
    }

    [Fact]
    public async Task GenerateAsync_WithV3Salon_ShouldDeclareTalkGroupCommandPrefix()
    {
        // Arrange — sans préfixe, LinkManager::addLogic ne crée aucun LinkCmd
        // (condition atoi(cmd) > 0) et aucune commande talkgroup n'est atteignable.
        var salon = CreateTestSalonV3();
        var outputPath = GetTestOutputPath("svxlink_linktoreflector_v3.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);

        iniData["LinkToReflector"]["CONNECT_LOGICS"]
            .Should().Be($"SimplexLogic:{DtmfTalkGroupCommands.Prefix},ReflectorLogic");
        iniData["LinkToReflector"]["DEFAULT_ACTIVE"].Should().Be("1");
        iniData["LinkToReflector"]["TIMEOUT"].Should().Be("0");
    }

    [Fact]
    public async Task GenerateAsync_WithV3Salon_ShouldDeclareANumericPrefixUsableByLinkManager()
    {
        // Arrange — le champ « commande » de CONNECT_LOGICS doit satisfaire atoi(cmd) > 0,
        // sinon SVXLink ignore silencieusement le préfixe.
        var salon = CreateTestSalonV3();
        var outputPath = GetTestOutputPath("svxlink_linktoreflector_v3_prefix.conf");

        // Act
        await _service.GenerateAsync(salon, outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);
        var simplexSpec = iniData["LinkToReflector"]["CONNECT_LOGICS"].Split(',')[0].Split(':');

        simplexSpec.Should().HaveCountGreaterThanOrEqualTo(2);
        int.TryParse(simplexSpec[1], out var command).Should().BeTrue();
        command.Should().BeGreaterThan(0);
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

    [Fact]
    public async Task GenerateAsync_WithParrotSalon_ShouldSetLogicsToSimplexLogicOnly()
    {
        // Arrange
        var salon = CreateTestParrotSalon();
        var outputPath = GetTestOutputPath("svxlink_parrot.conf");

        // Act
        var result = await _service.GenerateAsync(salon, outputPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var iniData = IniFile.Parse(outputPath);
        iniData["GLOBAL"]["LOGICS"].Should().Be("SimplexLogic");
        iniData["GLOBAL"].ContainsKey("LINKS").Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WithParrotSalon_ShouldGenerateModuleParrotSection()
    {
        // Arrange
        var salon = CreateTestParrotSalon();
        var outputPath = GetTestOutputPath("svxlink_parrot_module.conf");

        // Act
        var result = await _service.GenerateAsync(salon, outputPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var iniData = IniFile.Parse(outputPath);
        iniData["ModuleParrot"]["NAME"].Should().Be("Parrot");
        iniData["ModuleParrot"]["ID"].Should().Be("2");
        iniData["ModuleParrot"]["TIMEOUT"].Should().Be("180");
        iniData["ModuleParrot"]["FIFO_LEN"].Should().Be("60");
        iniData["ModuleParrot"]["REPEAT_DELAY"].Should().Be("1000");
    }

    [Fact]
    public async Task GenerateAsync_WithParrotSalon_ShouldNotContainReflectorLogicSection()
    {
        // Arrange
        var salon = CreateTestParrotSalon();
        var outputPath = GetTestOutputPath("svxlink_parrot_no_reflector.conf");

        // Act
        var result = await _service.GenerateAsync(salon, outputPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().NotContain("[ReflectorLogic]");
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

    private SalonAggregate CreateTestParrotSalon()
    {
        var configuration = new SvxLinkConfiguration(
            Id: Guid.NewGuid(),
            Logics: "SimplexLogic",
            CfgDir: "svxlink.d",
            CardSampleRate: 16000,
            CardChannels: 1,
            Host: "",
            Port: 0,
            Callsign: "",
            AuthKey: null,
            JitterBufferDelay: 0,
            ReflectorProtocol: ReflectorProtocol.V3,
            CertEmail: null,
            SimplexCallsign: "F0ABC",
            Modules: "ModuleParrot",
            ShortIdentInterval: 600,
            LongIdentInterval: 3600,
            ReportCtcss: null,
            DefaultLang: "fr_FR",
            RgrSoundDelay: 0,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: null,
            TxCtcss: null,
            ParrotFifoLen: 60,
            ParrotRepeatDelay: 1000,
            ParrotTimeout: 180
        );

        var result = SalonAggregate.Create(
            id: SalonAggregate.FixedParrotId,
            name: "Perroquet",
            isDefault: false,
            configuration: configuration,
            salonType: SalonType.Parrot
        );

        return result.Match(
            Succ: salon => salon,
            Fail: errors => throw new Exception($"Impossible de créer le Salon Perroquet de test: {errors}")
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
