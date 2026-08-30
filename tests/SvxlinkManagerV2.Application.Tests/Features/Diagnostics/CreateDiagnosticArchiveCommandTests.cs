using System.IO.Compression;
using System.Text;
using FluentAssertions;
using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SvxlinkManagerV2.Application.Features.Diagnostics;
using SvxlinkManagerV2.Application.Features.Diagnostics.CreateDiagnosticArchive;
using SvxlinkManagerV2.Application.Features.SystemStatus;
using SvxlinkManagerV2.Application.Features.SystemStatus.GetSystemStatus;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Wifi;

namespace SvxlinkManagerV2.Application.Tests.Features.Diagnostics;

/// <summary>
/// Tests unitaires de la constitution de l'archive de diagnostic : contenu attendu,
/// expurgation des secrets et dégradation propre lorsqu'une source est indisponible.
/// </summary>
public class CreateDiagnosticArchiveCommandTests
{
    private const string ConfigurationPath = "/etc/svxlink/svxlink.conf";

    private const string GeneratedConfiguration = """
        [GLOBAL]
        LOGICS=ReflectorLogic

        [ReflectorLogic]
        HOST=reflector.example.org
        PORT=5300
        CALLSIGN=F4ABC
        AUTH_KEY=Magnifique123456789!
        """;

    private readonly ISvxLinkLogService _svxLinkLogService = Substitute.For<ISvxLinkLogService>();
    private readonly IReflectorLogService _reflectorLogService = Substitute.For<IReflectorLogService>();
    private readonly ISvxLinkConfigurationReader _configurationReader = Substitute.For<ISvxLinkConfigurationReader>();
    private readonly ISender _sender = Substitute.For<ISender>();

    public CreateDiagnosticArchiveCommandTests()
    {
        _svxLinkLogService.GetLogs().Returns(
            [new SvxLinkLogEntry(new DateTime(2026, 8, 30, 14, 30, 0), "Connexion au reflector établie", SvxLinkLogLevel.Info)]);

        _reflectorLogService.GetLogs().Returns(
            [new SvxLinkLogEntry(new DateTime(2026, 8, 30, 14, 31, 0), "Nœud F4XYZ connecté", SvxLinkLogLevel.Info)]);

        _configurationReader.ConfigurationPath.Returns(ConfigurationPath);
        _configurationReader.ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<Error, string>>(GeneratedConfiguration));

        _sender.Send(Arg.Any<GetSystemStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(BuildSystemStatus()));
    }

    [Fact]
    public async Task Handle_ShouldProduceATimestampedZipArchive()
    {
        var result = await CreateHandler().Handle(new CreateDiagnosticArchiveCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var archive = result.Match(Succ: a => a, Fail: _ => null!);

        archive.FileName.Should().MatchRegex(@"^diagnostic-svxlinkmanager-\d{8}-\d{6}\.zip$");
        archive.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldBundleLogsConfigurationAndSystemInformation()
    {
        var entries = await ReadArchiveAsync();

        entries.Keys.Should().BeEquivalentTo(
            CreateDiagnosticArchiveCommandHandler.SvxLinkLogsEntryName,
            CreateDiagnosticArchiveCommandHandler.ReflectorLogsEntryName,
            CreateDiagnosticArchiveCommandHandler.ConfigurationEntryName,
            CreateDiagnosticArchiveCommandHandler.SystemInformationEntryName);

        entries[CreateDiagnosticArchiveCommandHandler.SvxLinkLogsEntryName]
            .Should().Contain("Connexion au reflector établie");

        entries[CreateDiagnosticArchiveCommandHandler.ReflectorLogsEntryName]
            .Should().Contain("Nœud F4XYZ connecté");

        entries[CreateDiagnosticArchiveCommandHandler.ConfigurationEntryName]
            .Should().Contain(ConfigurationPath)
            .And.Contain("HOST=reflector.example.org")
            .And.Contain("CALLSIGN=F4ABC");
    }

    [Fact]
    public async Task Handle_ShouldReportApplicationAndSvxLinkVersions()
    {
        var entries = await ReadArchiveAsync();

        var systemInformation = entries[CreateDiagnosticArchiveCommandHandler.SystemInformationEntryName];

        systemInformation.Should().Contain("Version de l'application : 1.1.0");
        systemInformation.Should().Contain("SVXLink legacy");
        systemInformation.Should().Contain("19.09.2");
        systemInformation.Should().Contain("SVXLink modern");
        systemInformation.Should().Contain("25.05");
        systemInformation.Should().Contain("48,3 °C");
    }

    [Fact]
    public async Task Handle_ShouldNotLeakAnyAuthenticationKey()
    {
        var entries = await ReadArchiveAsync();

        entries.Values.Should().NotContain(content => content.Contains("Magnifique123456789!"));

        entries[CreateDiagnosticArchiveCommandHandler.ConfigurationEntryName]
            .Should().Contain($"AUTH_KEY={DiagnosticSecretRedactor.RedactedValue}");
    }

    [Fact]
    public async Task Handle_ShouldNotLeakSecretsPresentInTheLogBuffers()
    {
        _svxLinkLogService.GetLogs().Returns(
            [new SvxLinkLogEntry(new DateTime(2026, 8, 30, 14, 30, 0), "ReflectorLogic: AUTH_KEY=Magnifique123456789!", SvxLinkLogLevel.Info)]);

        var entries = await ReadArchiveAsync();

        entries[CreateDiagnosticArchiveCommandHandler.SvxLinkLogsEntryName]
            .Should().NotContain("Magnifique123456789!")
            .And.Contain($"AUTH_KEY={DiagnosticSecretRedactor.RedactedValue}");
    }

    [Fact]
    public async Task Handle_ShouldStillProduceTheArchive_WhenConfigurationIsUnreadable()
    {
        _configurationReader.ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Error
                .Validation("SVXLINK_CONFIG_NOT_FOUND", $"Fichier de configuration introuvable : {ConfigurationPath}")
                .ToFailure<string>()));

        var entries = await ReadArchiveAsync();

        entries[CreateDiagnosticArchiveCommandHandler.ConfigurationEntryName]
            .Should().Contain("Configuration indisponible")
            .And.Contain("Fichier de configuration introuvable");
    }

    [Fact]
    public async Task Handle_ShouldStillProduceTheArchive_WhenSystemStatusCollectionFails()
    {
        _sender.Send(Arg.Any<GetSystemStatusQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("collecte impossible"));

        var entries = await ReadArchiveAsync();

        entries[CreateDiagnosticArchiveCommandHandler.SystemInformationEntryName]
            .Should().Contain("État système indisponible : collecte impossible");

        entries[CreateDiagnosticArchiveCommandHandler.SvxLinkLogsEntryName]
            .Should().Contain("Connexion au reflector établie");
    }

    /// <summary>
    /// Exécute la commande et retourne le contenu texte de chaque entrée de l'archive.
    /// </summary>
    private async Task<Dictionary<string, string>> ReadArchiveAsync()
    {
        var result = await CreateHandler().Handle(new CreateDiagnosticArchiveCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var content = result.Match(Succ: a => a.Content, Fail: _ => null!);

        using var buffer = new MemoryStream(content);
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);

        var entries = new Dictionary<string, string>();

        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            entries[entry.FullName] = await reader.ReadToEndAsync();
        }

        return entries;
    }

    private CreateDiagnosticArchiveCommandHandler CreateHandler()
        => new(
            _svxLinkLogService,
            _reflectorLogService,
            _configurationReader,
            _sender,
            Substitute.For<ILogger<CreateDiagnosticArchiveCommandHandler>>());

    private static SystemStatusDto BuildSystemStatus()
        => new(
            CollectedAt: new DateTimeOffset(2026, 8, 30, 14, 32, 11, TimeSpan.Zero),
            CpuTemperatureCelsius: SystemValueMetric.Available(48.3),
            CpuLoad: SystemMetric<CpuLoadMetrics>.Available(new CpuLoadMetrics(0.42, 0.38, 0.31, 4)),
            Memory: SystemMetric<MemoryMetrics>.Available(new MemoryMetrics(1024L * 1024 * 1024, 512L * 1024 * 1024)),
            Disks:
            [
                new DiskStatusDto(
                    "Partition système",
                    "/",
                    SystemMetric<DiskMetrics>.Available(new DiskMetrics("/", 16L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024))),
                new DiskStatusDto(
                    "Partition de données",
                    "/var/lib/svxlinkmanager",
                    SystemMetric<DiskMetrics>.Unavailable("Chemin absent sur cette plateforme"))
            ],
            Uptime: SystemMetric<UptimeMetrics>.Available(
                new UptimeMetrics(TimeSpan.FromHours(50), TimeSpan.FromMinutes(90))),
            Network: SystemMetric<WifiLink>.Available(
                new WifiLink(true, "wlan0", "MonReseau", 72, "10.0.0.10")),
            ApplicationVersion: "1.1.0",
            SvxLinkInstallations:
            [
                new SvxLinkInstallationDto("SVXLink legacy", "19.09.2", "V2", "/opt/svxlink-legacy/bin/svxlink", true),
                new SvxLinkInstallationDto("SVXLink modern", "25.05", "V3", "/opt/svxlink-modern/bin/svxlink", false)
            ]);
}
