using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.Common;
using SvxlinkManagerV2.Infrastructure.SvxLink;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests d'intégration pour SvxLinkParrotConfigurationService.
/// Valide la génération du fichier svxlink.conf en mode Perroquet avec le vrai template et ini-parser.
/// </summary>
public class SvxLinkParrotConfigurationServiceTests : IDisposable
{
    private readonly SvxLinkParrotConfigurationService _service;
    private readonly ILogger<SvxLinkParrotConfigurationService> _logger;
    private readonly string _testOutputDirectory;
    private readonly List<string> _filesToCleanup;
    private readonly string _templatePath;

    public SvxLinkParrotConfigurationServiceTests()
    {
        _logger = Substitute.For<ILogger<SvxLinkParrotConfigurationService>>();

        _templatePath = FindTemplatePath();
        _service = new SvxLinkParrotConfigurationService(_logger, _templatePath);

        _testOutputDirectory = Path.Combine(Path.GetTempPath(), $"svxlink-parrot-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testOutputDirectory);
        _filesToCleanup = new List<string>();
    }

    private static string FindTemplatePath()
    {
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
    public async Task GenerateAsync_ShouldCreateValidConfigurationFile()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_parrot.conf");

        // Act
        var result = await _service.GenerateAsync(outputPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();

        var iniData = IniFile.Parse(outputPath);
        iniData.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_ShouldSetGlobalLogicsToSimplexLogicOnly()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_parrot_global.conf");

        // Act
        await _service.GenerateAsync(outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);
        iniData["GLOBAL"]["LOGICS"].Should().Be("SimplexLogic");
    }

    [Fact]
    public async Task GenerateAsync_ShouldSetGlobalLinksToEmpty()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_parrot_links.conf");

        // Act
        await _service.GenerateAsync(outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);
        iniData["GLOBAL"]["LINKS"].Should().Be("");
    }

    [Fact]
    public async Task GenerateAsync_ShouldSetModuleParrotInSimplexLogic()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_parrot_simplex.conf");

        // Act
        await _service.GenerateAsync(outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);
        iniData["SimplexLogic"]["MODULES"].Should().Be("ModuleParrot");
    }

    [Fact]
    public async Task GenerateAsync_ShouldNotContainReflectorLogicSection()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_parrot_no_reflector.conf");

        // Act
        await _service.GenerateAsync(outputPath);

        // Assert — AC2 : pas de ReflectorLogic dans la config Perroquet
        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().NotContain("[ReflectorLogic]");
    }

    [Fact]
    public async Task GenerateAsync_ShouldNotContainLinkToReflectorSection()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_parrot_no_link.conf");

        // Act
        await _service.GenerateAsync(outputPath);

        // Assert — AC2 : pas de LinkToReflector dans la config Perroquet
        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().NotContain("[LinkToReflector]");
    }

    [Fact]
    public async Task GenerateAsync_ShouldPreserveHardwareSections()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_parrot_hardware.conf");

        // Act
        await _service.GenerateAsync(outputPath);

        // Assert — les sections hardware du template doivent être conservées
        var iniData = IniFile.Parse(outputPath);
        iniData["Rx1"]["TYPE"].Should().Be("Local");
        iniData["Rx1"]["AUDIO_DEV"].Should().NotBeNullOrEmpty();
        iniData["Tx1"]["TYPE"].Should().Be("Local");
        iniData["Tx1"]["AUDIO_DEV"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateAsync_ShouldPreserveSimplexLogicCallsign()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_parrot_callsign.conf");

        // Act
        await _service.GenerateAsync(outputPath);

        // Assert — le CALLSIGN du template doit être conservé
        var iniData = IniFile.Parse(outputPath);
        iniData["SimplexLogic"]["CALLSIGN"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateAsync_ShouldPreserveModuleParrotSection()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_parrot_module_section.conf");

        // Act
        await _service.GenerateAsync(outputPath);

        // Assert — la section [ModuleParrot] du template doit être conservée avec FIFO_LEN et REPEAT_DELAY
        var iniData = IniFile.Parse(outputPath);
        iniData.ContainsSection("ModuleParrot").Should().BeTrue();
        iniData["ModuleParrot"]["ID"].Should().Be("2");
        iniData["ModuleParrot"]["FIFO_LEN"].Should().NotBeNullOrEmpty();
        iniData["ModuleParrot"]["REPEAT_DELAY"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateAsync_ShouldWriteAtomically()
    {
        // Arrange
        var outputPath = GetTestOutputPath("svxlink_parrot_atomic.conf");
        var tempPath = $"{outputPath}.tmp";

        // Act
        await _service.GenerateAsync(outputPath);

        // Assert — aucun fichier .tmp résiduel
        File.Exists(outputPath).Should().BeTrue();
        File.Exists(tempPath).Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WhenTemplateNotFound_ShouldFail()
    {
        // Arrange
        var serviceWithBadTemplate = new SvxLinkParrotConfigurationService(
            _logger,
            "/chemin/inexistant/svxlink.conf");
        var outputPath = GetTestOutputPath("svxlink_parrot_notfound.conf");

        // Act
        var result = await serviceWithBadTemplate.GenerateAsync(outputPath);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_ShouldProduceValidIniSyntax()
    {
        // Arrange — AC6 : syntaxe INI correcte, sections obligatoires présentes
        var outputPath = GetTestOutputPath("svxlink_parrot_valid_ini.conf");

        // Act
        await _service.GenerateAsync(outputPath);

        // Assert
        var iniData = IniFile.Parse(outputPath);
        iniData.ContainsSection("GLOBAL").Should().BeTrue();
        iniData.ContainsSection("SimplexLogic").Should().BeTrue();
        iniData.ContainsSection("Rx1").Should().BeTrue();
        iniData.ContainsSection("Tx1").Should().BeTrue();
    }

    private string GetTestOutputPath(string fileName)
    {
        var path = Path.Combine(_testOutputDirectory, fileName);
        _filesToCleanup.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _filesToCleanup.Where(File.Exists))
        {
            try { File.Delete(file); }
            catch { }
        }

        if (Directory.Exists(_testOutputDirectory))
        {
            try { Directory.Delete(_testOutputDirectory, true); }
            catch { }
        }
    }
}
