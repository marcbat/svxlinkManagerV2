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
/// Tests unitaires pour CpuLoadInfoProvider.
/// </summary>
public class CpuLoadInfoProviderTests
{
    private readonly ISystemMetricsService _metrics;
    private readonly CpuLoadInfoProvider _provider;

    public CpuLoadInfoProviderTests()
    {
        _metrics = Substitute.For<ISystemMetricsService>();
        _provider = new CpuLoadInfoProvider(
            _metrics,
            Substitute.For<ILogger<CpuLoadInfoProvider>>());
    }

    [Fact]
    public void DtmfCode_ShouldBe302()
    {
        _provider.DtmfCode.Should().Be(302);
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        _provider.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(1.0d, 4, "La charge du processeur est de 25 pour cent")]
    [InlineData(0.5d, 1, "La charge du processeur est de 50 pour cent")]
    [InlineData(2.0d, 1, "La charge du processeur est de 200 pour cent")]
    [InlineData(0d, 4, "La charge du processeur est de 0 pour cent")]
    public async Task GetInfoTextAsync_WhenLoadAvailable_ShouldReturnFrenchSentence(
        double load1,
        int coreCount,
        string expectedText)
    {
        _metrics.GetCpuLoadAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, CpuLoadMetrics>.Success(
                new CpuLoadMetrics(load1, load1, load1, coreCount)));

        var result = await _provider.GetInfoTextAsync();

        result.IsSuccess.Should().BeTrue();
        result.IfSuccess(text => text.Should().Be(expectedText));
    }

    [Fact]
    public async Task GetInfoTextAsync_WhenLoadUnavailable_ShouldReturnFailure()
    {
        _metrics.GetCpuLoadAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, CpuLoadMetrics>.Fail(
                Prelude.Seq1(Error.Validation("SYSTEM_CPU_LOAD_UNAVAILABLE", "Source absente"))));

        var result = await _provider.GetInfoTextAsync();

        result.IsFail.Should().BeTrue();
    }
}
