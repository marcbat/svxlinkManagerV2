using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.Monitoring;

namespace SvxlinkManagerV2.Infrastructure.Tests.Monitoring;

/// <summary>
/// Tests unitaires pour LinuxSystemMetricsService.
/// Les pseudo-fichiers /proc et /sys sont simulés par des fichiers temporaires réels,
/// afin que les tests s'exécutent sur toute plateforme.
/// </summary>
public class LinuxSystemMetricsServiceTests : IDisposable
{
    private readonly ILogger<LinuxSystemMetricsService> _logger;
    private readonly string _tempDirectory;

    public LinuxSystemMetricsServiceTests()
    {
        _logger = Substitute.For<ILogger<LinuxSystemMetricsService>>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"sysmetrics_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDirectory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private string MissingPath(string name) => Path.Combine(_tempDirectory, $"missing_{name}");

    private LinuxSystemMetricsService CreateService(SystemMetricsPaths paths)
        => new(_logger, paths);

    // -------------------------------------------------------------------------
    // Température CPU
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("45000", 45d)]
    [InlineData("42500", 42.5d)]
    [InlineData("0", 0d)]
    [InlineData("75000\n", 75d)]
    public async Task GetCpuTemperatureCelsiusAsync_WithValidContent_ShouldConvertFromMilliDegrees(
        string content,
        double expected)
    {
        var service = CreateService(new SystemMetricsPaths { ThermalZone = WriteFile("temp", content) });

        var result = await service.GetCpuTemperatureCelsiusAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(celsius => celsius.Should().Be(expected));
    }

    [Theory]
    [InlineData("not_a_number")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCpuTemperatureCelsiusAsync_WithInvalidContent_ShouldFail(string content)
    {
        var service = CreateService(new SystemMetricsPaths { ThermalZone = WriteFile("temp", content) });

        var result = await service.GetCpuTemperatureCelsiusAsync();

        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task GetCpuTemperatureCelsiusAsync_WhenSourceMissing_ShouldFail()
    {
        var service = CreateService(new SystemMetricsPaths { ThermalZone = MissingPath("temp") });

        var result = await service.GetCpuTemperatureCelsiusAsync();

        result.IsFail.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Charge processeur
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetCpuLoadAsync_WithValidLoadAvg_ShouldParseThreeAverages()
    {
        var service = CreateService(new SystemMetricsPaths
        {
            LoadAvg = WriteFile("loadavg", "0.52 0.41 0.38 1/234 5678\n")
        });

        var result = await service.GetCpuLoadAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(load =>
        {
            load.Load1.Should().Be(0.52);
            load.Load5.Should().Be(0.41);
            load.Load15.Should().Be(0.38);
            load.CoreCount.Should().Be(Environment.ProcessorCount);
        });
    }

    [Fact]
    public async Task GetCpuLoadAsync_WithMalformedContent_ShouldFail()
    {
        var service = CreateService(new SystemMetricsPaths { LoadAvg = WriteFile("loadavg", "abc def") });

        var result = await service.GetCpuLoadAsync();

        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public async Task GetCpuLoadAsync_WhenSourceMissing_ShouldFail()
    {
        var service = CreateService(new SystemMetricsPaths { LoadAvg = MissingPath("loadavg") });

        var result = await service.GetCpuLoadAsync();

        result.IsFail.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Mémoire
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMemoryAsync_WithMemAvailable_ShouldUseIt()
    {
        var content = string.Join('\n',
            "MemTotal:        1017576 kB",
            "MemFree:          100000 kB",
            "MemAvailable:     500000 kB",
            "Buffers:           20000 kB",
            "Cached:           300000 kB");

        var service = CreateService(new SystemMetricsPaths { MemInfo = WriteFile("meminfo", content) });

        var result = await service.GetMemoryAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(memory =>
        {
            memory.TotalBytes.Should().Be(1017576L * 1024);
            memory.AvailableBytes.Should().Be(500000L * 1024);
            memory.UsedPercent.Should().BeApproximately(50.86d, 0.1d);
        });
    }

    [Fact]
    public async Task GetMemoryAsync_WithoutMemAvailable_ShouldFallBackOnFreeBuffersCached()
    {
        var content = string.Join('\n',
            "MemTotal:        1000000 kB",
            "MemFree:          100000 kB",
            "Buffers:           50000 kB",
            "Cached:           150000 kB");

        var service = CreateService(new SystemMetricsPaths { MemInfo = WriteFile("meminfo", content) });

        var result = await service.GetMemoryAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(memory => memory.AvailableBytes.Should().Be(300000L * 1024));
    }

    [Fact]
    public async Task GetMemoryAsync_WithoutMemTotal_ShouldFail()
    {
        var service = CreateService(new SystemMetricsPaths
        {
            MemInfo = WriteFile("meminfo", "MemFree:          100000 kB")
        });

        var result = await service.GetMemoryAsync();

        result.IsFail.Should().BeTrue();
    }

    [Fact]
    public void ParseMemInfo_ShouldIgnoreMalformedLines()
    {
        var values = LinuxSystemMetricsService.ParseMemInfo(
            "MemTotal:        1000 kB\nligne sans separateur\nBogus: pas_un_nombre\nMemFree: 500 kB");

        values.Should().ContainKey("MemTotal").WhoseValue.Should().Be(1000);
        values.Should().ContainKey("MemFree").WhoseValue.Should().Be(500);
        values.Should().NotContainKey("Bogus");
    }

    // -------------------------------------------------------------------------
    // Uptime
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetUptimeAsync_WithValidProcUptime_ShouldParseMachineUptime()
    {
        var service = CreateService(new SystemMetricsPaths
        {
            Uptime = WriteFile("uptime", "123456.78 234567.89\n")
        });

        var result = await service.GetUptimeAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(uptime =>
        {
            uptime.Machine.TotalSeconds.Should().BeApproximately(123456.78, 0.01);
            uptime.Process.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task GetUptimeAsync_WhenSourceMissing_ShouldFallBackOnRuntimeTicks()
    {
        var service = CreateService(new SystemMetricsPaths { Uptime = MissingPath("uptime") });

        var result = await service.GetUptimeAsync();

        // L'uptime reste disponible sur une plateforme dépourvue de /proc.
        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(uptime => uptime.Machine.Should().BeGreaterThan(TimeSpan.Zero));
    }

    [Fact]
    public async Task GetUptimeAsync_WithMalformedContent_ShouldFallBackOnRuntimeTicks()
    {
        var service = CreateService(new SystemMetricsPaths { Uptime = WriteFile("uptime", "pas un nombre") });

        var result = await service.GetUptimeAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(uptime => uptime.Machine.Should().BeGreaterThan(TimeSpan.Zero));
    }

    // -------------------------------------------------------------------------
    // Disque
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDiskAsync_OnExistingDirectory_ShouldReturnPartitionUsage()
    {
        var service = CreateService(new SystemMetricsPaths());

        var result = await service.GetDiskAsync(_tempDirectory);

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(disk =>
        {
            disk.MountPath.Should().Be(_tempDirectory);
            disk.TotalBytes.Should().BeGreaterThan(0);
            disk.AvailableBytes.Should().BeGreaterThanOrEqualTo(0);
            disk.UsedPercent.Should().BeInRange(0d, 100d);
        });
    }

    [Fact]
    public async Task GetDiskAsync_OnNotYetCreatedDirectory_ShouldResolveExistingParent()
    {
        // Le répertoire de données peut ne pas exister au premier démarrage.
        var notCreated = Path.Combine(_tempDirectory, "data", "updates");

        var service = CreateService(new SystemMetricsPaths());

        var result = await service.GetDiskAsync(notCreated);

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(disk => disk.TotalBytes.Should().BeGreaterThan(0));
    }

    [Fact]
    public void ResolveExistingPath_ShouldWalkUpToFirstExistingDirectory()
    {
        var missing = Path.Combine(_tempDirectory, "a", "b", "c");

        var resolved = LinuxSystemMetricsService.ResolveExistingPath(missing);

        resolved.Should().Be(Path.GetFullPath(_tempDirectory));
    }

    // -------------------------------------------------------------------------
    // Version applicative
    // -------------------------------------------------------------------------

    [Fact]
    public void GetApplicationVersion_ShouldReturnVersionWithoutBuildMetadata()
    {
        var service = CreateService(new SystemMetricsPaths());

        var version = service.GetApplicationVersion();

        version.Should().NotBeNullOrWhiteSpace();
        version.Should().NotContain("+");
    }
}
