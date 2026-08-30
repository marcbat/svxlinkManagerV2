using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SvxlinkManagerV2.Infrastructure.Hardware;

namespace SvxlinkManagerV2.Infrastructure.Tests.Hardware;

/// <summary>
/// Tests de l'implémentation simulée des niveaux ALSA.
/// </summary>
public class AudioMockServiceTests
{
    [Fact]
    public void IsSimulated_ShouldBeTrue()
    {
        CreateService().IsSimulated.Should().BeTrue();
    }

    [Fact]
    public async Task GetStateAsync_ShouldExposeBothConfiguredControls()
    {
        var service = CreateService();

        var result = await service.GetStateAsync();

        result.ShouldBeSuccess(state =>
        {
            state.Capture.Name.Should().Be("ADC Gain");
            state.Playback.Name.Should().Be("Line Out");
            state.IsSimulated.Should().BeTrue();
        });
    }

    [Fact]
    public async Task SetCaptureLevelAsync_ShouldBeVisibleFromGetStateAsync()
    {
        var service = CreateService();

        await service.SetCaptureLevelAsync(5);
        var result = await service.GetStateAsync();

        result.ShouldBeSuccess(state => state.Capture.Value.Should().Be(5));
    }

    [Fact]
    public async Task SetPlaybackLevelAsync_ShouldBeVisibleFromGetStateAsync()
    {
        var service = CreateService();

        await service.SetPlaybackLevelAsync(12);
        var result = await service.GetStateAsync();

        result.ShouldBeSuccess(state => state.Playback.Value.Should().Be(12));
    }

    [Fact]
    public async Task SetCaptureLevelAsync_ShouldClampAboveTheRange()
    {
        var result = await CreateService().SetCaptureLevelAsync(99);

        result.ShouldBeSuccess(state => state.Value.Should().Be(state.MaxValue));
    }

    [Fact]
    public async Task SetPlaybackLevelAsync_ShouldClampBelowTheRange()
    {
        var result = await CreateService().SetPlaybackLevelAsync(-4);

        result.ShouldBeSuccess(state => state.Value.Should().Be(state.MinValue));
    }

    private static AudioMockService CreateService() =>
        new(Options.Create(new AudioOptions()), Substitute.For<ILogger<AudioMockService>>());
}
