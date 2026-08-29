using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests unitaires pour VoiceAnnouncementService
/// </summary>
public class VoiceAnnouncementServiceTests
{
    private readonly ITtsService _ttsService;
    private readonly IDtmfPtyWriter _ptyWriter;
    private readonly ILogger<VoiceAnnouncementService> _logger;

    public VoiceAnnouncementServiceTests()
    {
        _ttsService = Substitute.For<ITtsService>();
        _ptyWriter = Substitute.For<IDtmfPtyWriter>();
        _logger = Substitute.For<ILogger<VoiceAnnouncementService>>();
    }

    private VoiceAnnouncementService CreateService() => new(_ttsService, _ptyWriter, _logger);

    [Fact]
    public async Task AnnounceAsync_ShouldGenerateWavThenTriggerPlayback()
    {
        _ttsService.GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, string>.Success(VoiceAnnouncementService.TtsWavPath));
        _ptyWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, Unit>.Success(Unit.Default));

        var result = await CreateService().AnnounceAsync("Salon déconnecté.");

        result.ShouldBeSuccess();
        await _ttsService.Received(1).GenerateWavAsync(
            "Salon déconnecté.",
            VoiceAnnouncementService.TtsWavPath,
            Arg.Any<CancellationToken>());
        await _ptyWriter.Received(1).SendCommandAsync(
            VoiceAnnouncementService.TtsInternalCode.ToString(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnounceAsync_WhenTtsFails_ShouldNotTriggerPlayback()
    {
        _ttsService.GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, string>.Fail(Seq1(Error.New("pico2wave introuvable"))));

        var result = await CreateService().AnnounceAsync("Texte");

        result.ShouldBeFail();
        await _ptyWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnounceAsync_WhenPtyFails_ShouldReturnError()
    {
        _ttsService.GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, string>.Success(VoiceAnnouncementService.TtsWavPath));
        _ptyWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, Unit>.Fail(Seq1(Error.New("PTY introuvable"))));

        var result = await CreateService().AnnounceAsync("Texte");

        result.ShouldBeFail();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnnounceAsync_WithEmptyText_ShouldFailWithoutCallingTts(string text)
    {
        var result = await CreateService().AnnounceAsync(text);

        result.ShouldBeFail();
        await _ttsService.DidNotReceive().GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constants_ShouldMatchLogicTclContract()
    {
        Assert.Equal(399, VoiceAnnouncementService.TtsInternalCode);
        Assert.Equal("/tmp/svxlink_tts.wav", VoiceAnnouncementService.TtsWavPath);
    }
}
