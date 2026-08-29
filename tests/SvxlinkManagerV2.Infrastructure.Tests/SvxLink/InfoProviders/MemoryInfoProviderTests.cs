using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink.InfoProviders;

/// <summary>
/// Tests unitaires pour MemoryInfoProvider.
/// </summary>
public class MemoryInfoProviderTests
{
    private const long Megabyte = 1024L * 1024L;

    private readonly ISystemMetricsService _metrics;
    private readonly MemoryInfoProvider _provider;

    public MemoryInfoProviderTests()
    {
        _metrics = Substitute.For<ISystemMetricsService>();
        _provider = new MemoryInfoProvider(
            _metrics,
            Substitute.For<ILogger<MemoryInfoProvider>>());
    }

    [Fact]
    public void DtmfCode_ShouldBe307()
    {
        _provider.DtmfCode.Should().Be(307);
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        _provider.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetInfoTextAsync_WithMegabyteRange_ShouldAnnounceMegabytes()
    {
        _metrics.GetMemoryAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, MemoryMetrics>.Success(
                new MemoryMetrics(512 * Megabyte, 128 * Megabyte)));

        var result = await _provider.GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(text => text.Should().Be(
            "La mémoire disponible est de 128 mégaoctets sur 512 mégaoctets, soit 75 pour cent utilisés"));
    }

    [Fact]
    public async Task GetInfoTextAsync_WithGigabyteRange_ShouldAnnounceGigabytes()
    {
        _metrics.GetMemoryAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, MemoryMetrics>.Success(
                new MemoryMetrics(2048 * Megabyte, 1024 * Megabyte)));

        var result = await _provider.GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(text => text.Should().Be(
            "La mémoire disponible est de 1 gigaoctet sur 2 gigaoctets, soit 50 pour cent utilisés"));
    }

    [Fact]
    public async Task GetInfoTextAsync_WhenMemoryUnavailable_ShouldReturnFailure()
    {
        _metrics.GetMemoryAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, MemoryMetrics>.Fail(
                Prelude.Seq1(Error.Validation("SYSTEM_MEMORY_UNAVAILABLE", "Source absente"))));

        var result = await _provider.GetInfoTextAsync();

        result.IsFail.Should().BeTrue();
    }
}
