using FluentAssertions;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Infrastructure.Hardware;

namespace SvxlinkManagerV2.Infrastructure.Tests.Hardware;

/// <summary>
/// Tests de l'analyse de la sortie d'<c>amixer</c>.
///
/// Les sorties utilisées ici sont celles relevées sur la plateforme cible : Orange Pi Zero,
/// carte 0 « H3 Audio Codec », noyau 5.10.43-sunxi. Elles couvrent les trois formes de ligne de
/// valeur qu'amixer produit — canaux stéréo, canal mono avec sens de flux, canal mono sans sens.
/// </summary>
public class AlsaAudioServiceTests
{
    private const string LineOutOutput = """
        Simple mixer control 'Line Out',0
          Capabilities: pvolume pvolume-joined pswitch
          Playback channels: Front Left - Front Right
          Limits: Playback 0 - 31
          Mono:
          Front Left: Playback 22 [71%] [-13.50dB] [on]
          Front Right: Playback 22 [71%] [-13.50dB] [on]
        """;

    private const string AdcGainOutput = """
        Simple mixer control 'ADC Gain',0
          Capabilities: cvolume cvolume-joined
          Capture channels: Mono
          Limits: Capture 0 - 7
          Mono: Capture 3 [43%] [0.00dB]
        """;

    private const string Mic1BoostOutput = """
        Simple mixer control 'Mic1 Boost',0
          Capabilities: volume volume-joined
          Playback channels: Mono
          Capture channels: Mono
          Limits: 0 - 7
          Mono: 1 [14%] [24.00dB]
        """;

    private const string SwitchOnlyOutput = """
        Simple mixer control 'Mixer',0
          Capabilities: cswitch
          Capture channels: Front Left - Front Right
          Front Left: Capture [off]
          Front Right: Capture [off]
        """;

    [Fact]
    public void ParseControl_ShouldReadStereoPlaybackControl()
    {
        var result = AlsaAudioService.ParseControl("Line Out", LineOutOutput);

        result.ShouldBeSuccess(state =>
        {
            state.Name.Should().Be("Line Out");
            state.MinValue.Should().Be(0);
            state.MaxValue.Should().Be(31);
            state.Value.Should().Be(22);
        });
    }

    [Fact]
    public void ParseControl_ShouldReadMonoCaptureControl()
    {
        var result = AlsaAudioService.ParseControl("ADC Gain", AdcGainOutput);

        result.ShouldBeSuccess(state =>
        {
            state.MinValue.Should().Be(0);
            state.MaxValue.Should().Be(7);
            state.Value.Should().Be(3);
        });
    }

    [Fact]
    public void ParseControl_ShouldReadControlWithoutDirectionOnItsValueLine()
    {
        // « Mic1 Boost » ne préfixe sa valeur ni de Playback ni de Capture.
        var result = AlsaAudioService.ParseControl("Mic1 Boost", Mic1BoostOutput);

        result.ShouldBeSuccess(state =>
        {
            state.MaxValue.Should().Be(7);
            state.Value.Should().Be(1);
        });
    }

    [Fact]
    public void ParseControl_ShouldNotMistakeTheLimitsLineForAValue()
    {
        var result = AlsaAudioService.ParseControl("Line Out", LineOutOutput);

        // Sans la contrainte du crochet ouvrant, « Limits: Playback 0 - 31 » donnerait 0.
        result.ShouldBeSuccess(state => state.Value.Should().Be(22));
    }

    [Fact]
    public void ParseControl_ShouldFail_ForASwitchOnlyControl()
    {
        var result = AlsaAudioService.ParseControl("Mixer", SwitchOnlyOutput);

        result.ShouldBeFail(errors =>
            errors.Should().Contain(error => error.Code == "AUDIO_CONTROL_NOT_ADJUSTABLE"));
    }

    [Fact]
    public void ParseControl_ShouldFail_WhenTheValueLineIsMissing()
    {
        var truncated = """
            Simple mixer control 'Line Out',0
              Capabilities: pvolume pvolume-joined pswitch
              Limits: Playback 0 - 31
            """;

        var result = AlsaAudioService.ParseControl("Line Out", truncated);

        result.ShouldBeFail(errors =>
            errors.Should().Contain(error => error.Code == "AUDIO_CONTROL_UNREADABLE"));
    }

    [Fact]
    public void ParseControl_ShouldFail_OnEmptyOutput()
    {
        var result = AlsaAudioService.ParseControl("Line Out", string.Empty);

        result.ShouldBeFail();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(22, 71)]
    [InlineData(31, 100)]
    public void Percent_ShouldPositionTheValueInItsRange(int value, int expectedPercent)
    {
        var output = LineOutOutput.Replace("Playback 22 [71%]", $"Playback {value} [0%]");

        var result = AlsaAudioService.ParseControl("Line Out", output);

        result.ShouldBeSuccess(state => state.Percent.Should().Be(expectedPercent));
    }
}
