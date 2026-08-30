using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.Options;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.SystemStatus;
using SvxlinkManagerV2.Application.Features.SystemStatus.GetSystemStatus;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Wifi;

namespace SvxlinkManagerV2.Application.Tests.Features.SystemStatus;

/// <summary>
/// Tests unitaires pour GetSystemStatusQueryHandler.
/// </summary>
public class GetSystemStatusQueryHandlerTests
{
    private const long Gigabyte = 1024L * 1024L * 1024L;

    private readonly ISystemMetricsService _metrics;
    private readonly IWifiService _wifiService;
    private readonly ISvxLinkStrategyResolver _strategyResolver;
    private readonly SystemMonitoringOptions _options;
    private readonly GetSystemStatusQueryHandler _handler;

    public GetSystemStatusQueryHandlerTests()
    {
        _metrics = Substitute.For<ISystemMetricsService>();
        _wifiService = Substitute.For<IWifiService>();
        _strategyResolver = Substitute.For<ISvxLinkStrategyResolver>();
        _options = new SystemMonitoringOptions
        {
            SystemMountPath = "/",
            DataPath = "data",
            CpuTemperatureWarningCelsius = 65,
            CpuTemperatureCriticalCelsius = 75,
            DiskUsageWarningPercent = 80,
            DiskUsageCriticalPercent = 90,
            MemoryUsageWarningPercent = 85,
            MemoryUsageCriticalPercent = 95,
            WifiSignalWarningPercent = 40
        };

        _strategyResolver.GetAll().Returns(Array.Empty<ISvxLinkVersionStrategy>());
        _metrics.GetApplicationVersion().Returns("1.2.3");

        SetAllMetricsUnavailable();

        _handler = new GetSystemStatusQueryHandler(
            _metrics,
            _wifiService,
            _strategyResolver,
            Options.Create(_options));
    }

    private void SetAllMetricsUnavailable()
    {
        _metrics.GetCpuTemperatureCelsiusAsync(Arg.Any<CancellationToken>())
            .Returns(Fail<double>("SYSTEM_TEMPERATURE_UNAVAILABLE", "Capteur absent"));
        _metrics.GetCpuLoadAsync(Arg.Any<CancellationToken>())
            .Returns(Fail<CpuLoadMetrics>("SYSTEM_CPU_LOAD_UNAVAILABLE", "Source absente"));
        _metrics.GetMemoryAsync(Arg.Any<CancellationToken>())
            .Returns(Fail<MemoryMetrics>("SYSTEM_MEMORY_UNAVAILABLE", "Source absente"));
        _metrics.GetUptimeAsync(Arg.Any<CancellationToken>())
            .Returns(Fail<UptimeMetrics>("SYSTEM_UPTIME_UNAVAILABLE", "Source absente"));
        _metrics.GetDiskAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Fail<DiskMetrics>("SYSTEM_DISK_UNAVAILABLE", "Partition introuvable"));
        _wifiService.GetActiveLinkAsync(Arg.Any<CancellationToken>())
            .Returns(Fail<WifiLink>("WIFI_COMMAND_FAILED", "nmcli absent"));
    }

    private static Validation<Error, T> Fail<T>(string code, string message)
        => Validation<Error, T>.Fail(Prelude.Seq1(Error.Validation(code, message)));

    // -------------------------------------------------------------------------
    // Agrégation nominale
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WhenAllMetricsAvailable_ShouldExposeThem()
    {
        _metrics.GetCpuTemperatureCelsiusAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, double>.Success(48.5));
        _metrics.GetCpuLoadAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, CpuLoadMetrics>.Success(new CpuLoadMetrics(0.4, 0.3, 0.2, 4)));
        _metrics.GetMemoryAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, MemoryMetrics>.Success(new MemoryMetrics(Gigabyte, Gigabyte / 2)));
        _metrics.GetUptimeAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, UptimeMetrics>.Success(
                new UptimeMetrics(TimeSpan.FromHours(30), TimeSpan.FromMinutes(15))));
        _metrics.GetDiskAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, DiskMetrics>.Success(new DiskMetrics("/", 16 * Gigabyte, 8 * Gigabyte)));
        _wifiService.GetActiveLinkAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, WifiLink>.Success(
                new WifiLink(true, "wlan0", "HomeNetwork", 80, "192.168.1.42")));

        var status = await _handler.Handle(new GetSystemStatusQuery(), CancellationToken.None);

        status.CpuTemperatureCelsius.Value.Should().Be(48.5);
        status.CpuTemperatureCelsius.Level.Should().Be(MetricLevel.Normal);
        status.CpuLoad.Value!.LoadPercent.Should().Be(10d);
        status.Memory.Value!.UsedPercent.Should().Be(50d);
        status.Uptime.Value!.Machine.Should().Be(TimeSpan.FromHours(30));
        status.Network.Value!.Ssid.Should().Be("HomeNetwork");
        status.ApplicationVersion.Should().Be("1.2.3");
        status.Disks.Should().HaveCount(2);
        status.Disks.Should().OnlyContain(d => d.Metric.IsAvailable);
    }

    // -------------------------------------------------------------------------
    // Métriques indisponibles
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_WhenEveryMetricFails_ShouldStillReturnStatus()
    {
        var status = await _handler.Handle(new GetSystemStatusQuery(), CancellationToken.None);

        status.Should().NotBeNull();
        status.CpuTemperatureCelsius.IsAvailable.Should().BeFalse();
        status.CpuTemperatureCelsius.UnavailableReason.Should().Be("Capteur absent");
        status.CpuLoad.IsAvailable.Should().BeFalse();
        status.Memory.IsAvailable.Should().BeFalse();
        status.Uptime.IsAvailable.Should().BeFalse();
        status.Network.IsAvailable.Should().BeFalse();
        status.Disks.Should().OnlyContain(d => !d.Metric.IsAvailable);
    }

    [Fact]
    public async Task Handle_WhenOnlyTemperatureAvailable_ShouldNotInvalidateIt()
    {
        _metrics.GetCpuTemperatureCelsiusAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, double>.Success(50d));

        var status = await _handler.Handle(new GetSystemStatusQuery(), CancellationToken.None);

        status.CpuTemperatureCelsius.IsAvailable.Should().BeTrue();
        status.Memory.IsAvailable.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Seuils d'alerte
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(50d, MetricLevel.Normal)]
    [InlineData(64.9d, MetricLevel.Normal)]
    [InlineData(65d, MetricLevel.Warning)]
    [InlineData(74.9d, MetricLevel.Warning)]
    [InlineData(75d, MetricLevel.Critical)]
    [InlineData(90d, MetricLevel.Critical)]
    public async Task Handle_ShouldFlagTemperatureAgainstConfiguredThresholds(
        double celsius,
        MetricLevel expected)
    {
        _metrics.GetCpuTemperatureCelsiusAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, double>.Success(celsius));

        var status = await _handler.Handle(new GetSystemStatusQuery(), CancellationToken.None);

        status.CpuTemperatureCelsius.Level.Should().Be(expected);
    }

    [Theory]
    [InlineData(50d, MetricLevel.Normal)]
    [InlineData(85d, MetricLevel.Warning)]
    [InlineData(95d, MetricLevel.Critical)]
    public async Task Handle_ShouldFlagDiskUsageAgainstConfiguredThresholds(
        double usedPercent,
        MetricLevel expected)
    {
        const long total = 100 * Gigabyte;
        var available = (long)(total * (100d - usedPercent) / 100d);

        _metrics.GetDiskAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, DiskMetrics>.Success(new DiskMetrics("/", total, available)));

        var status = await _handler.Handle(new GetSystemStatusQuery(), CancellationToken.None);

        status.Disks.Should().OnlyContain(d => d.Metric.Level == expected);
    }

    [Fact]
    public async Task Handle_WithWeakWifiSignal_ShouldFlagWarning()
    {
        _wifiService.GetActiveLinkAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, WifiLink>.Success(
                new WifiLink(true, "wlan0", "HomeNetwork", 25, "192.168.1.42")));

        var status = await _handler.Handle(new GetSystemStatusQuery(), CancellationToken.None);

        status.Network.Level.Should().Be(MetricLevel.Warning);
    }

    // -------------------------------------------------------------------------
    // Partitions et installations SVXLink
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_ShouldQueryBothConfiguredPartitions()
    {
        var status = await _handler.Handle(new GetSystemStatusQuery(), CancellationToken.None);

        status.Disks.Select(d => d.Path).Should().Equal("/", "data");
        await _metrics.Received(1).GetDiskAsync("/", Arg.Any<CancellationToken>());
        await _metrics.Received(1).GetDiskAsync("data", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldExposeRegisteredSvxLinkInstallations()
    {
        var legacy = Substitute.For<ISvxLinkVersionStrategy>();
        legacy.Protocol.Returns(ReflectorProtocol.V2);
        legacy.DisplayName.Returns("SVXLink legacy");
        legacy.Version.Returns("19.09.2");
        legacy.BinaryPath.Returns("/opt/svxlink-legacy/bin/svxlink");
        legacy.IsInstalled.Returns(true);

        var modern = Substitute.For<ISvxLinkVersionStrategy>();
        modern.Protocol.Returns(ReflectorProtocol.V3);
        modern.DisplayName.Returns("SVXLink modern");
        modern.Version.Returns("25.05");
        modern.BinaryPath.Returns("/opt/svxlink-modern/bin/svxlink");
        modern.IsInstalled.Returns(false);

        _strategyResolver.GetAll().Returns(new[] { modern, legacy });

        var status = await _handler.Handle(new GetSystemStatusQuery(), CancellationToken.None);

        status.SvxLinkInstallations.Should().HaveCount(2);
        status.SvxLinkInstallations[0].Version.Should().Be("19.09.2");
        status.SvxLinkInstallations[0].IsInstalled.Should().BeTrue();
        status.SvxLinkInstallations[1].Version.Should().Be("25.05");
        status.SvxLinkInstallations[1].IsInstalled.Should().BeFalse();
    }
}
