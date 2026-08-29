using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.SystemStatus;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink.InfoProviders;

/// <summary>
/// Tests unitaires pour DiskSpaceInfoProvider.
/// </summary>
public class DiskSpaceInfoProviderTests
{
    private const long Gigabyte = 1024L * 1024L * 1024L;

    private readonly ISystemMetricsService _metrics;
    private readonly SystemMonitoringOptions _options;
    private readonly DiskSpaceInfoProvider _provider;

    public DiskSpaceInfoProviderTests()
    {
        _metrics = Substitute.For<ISystemMetricsService>();
        _options = new SystemMonitoringOptions { SystemMountPath = "/" };
        _provider = new DiskSpaceInfoProvider(
            _metrics,
            Options.Create(_options),
            Substitute.For<ILogger<DiskSpaceInfoProvider>>());
    }

    [Fact]
    public void DtmfCode_ShouldBe304()
    {
        _provider.DtmfCode.Should().Be(304);
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        _provider.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetInfoTextAsync_WhenDiskAvailable_ShouldReturnFrenchSentence()
    {
        _metrics.GetDiskAsync("/", Arg.Any<CancellationToken>())
            .Returns(Validation<Error, DiskMetrics>.Success(
                new DiskMetrics("/", 16 * Gigabyte, 4 * Gigabyte)));

        var result = await _provider.GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(text => text.Should().Be(
            "L'espace disque disponible est de 4 gigaoctets sur 16 gigaoctets, soit 75 pour cent utilisés"));
    }

    [Fact]
    public async Task GetInfoTextAsync_ShouldQueryConfiguredSystemMountPath()
    {
        _options.SystemMountPath = "/mnt/data";
        _metrics.GetDiskAsync("/mnt/data", Arg.Any<CancellationToken>())
            .Returns(Validation<Error, DiskMetrics>.Success(
                new DiskMetrics("/mnt/data", 8 * Gigabyte, 8 * Gigabyte)));

        var result = await _provider.GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        await _metrics.Received(1).GetDiskAsync("/mnt/data", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetInfoTextAsync_WhenDiskUnavailable_ShouldReturnFailure()
    {
        _metrics.GetDiskAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, DiskMetrics>.Fail(
                Prelude.Seq1(Error.Validation("SYSTEM_DISK_UNAVAILABLE", "Partition introuvable"))));

        var result = await _provider.GetInfoTextAsync();

        result.IsFail.Should().BeTrue();
    }
}
