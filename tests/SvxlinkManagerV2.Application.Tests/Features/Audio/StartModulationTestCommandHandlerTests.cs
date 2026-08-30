using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Audio.StartModulationTest;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using LanguageExtError = LanguageExt.Common.Error;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Tests.Features.Audio;

/// <summary>
/// Tests unitaires de l'annonce vocale de test diffusée depuis la page de réglage audio.
/// </summary>
public class StartModulationTestCommandHandlerTests
{
    private readonly IVoiceAnnouncementService _announcementService = Substitute.For<IVoiceAnnouncementService>();
    private readonly IPttTestService _pttTestService = Substitute.For<IPttTestService>();
    private readonly IActiveSessionTracker _tracker = Substitute.For<IActiveSessionTracker>();
    private readonly ISvxLinkDaemonService _daemonService = Substitute.For<ISvxLinkDaemonService>();

    private readonly StartModulationTestCommandHandler _handler;

    public StartModulationTestCommandHandlerTests()
    {
        _tracker.ActiveSalonId.Returns(Guid.NewGuid());
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<LanguageExtError, bool>.Success(true));
        _pttTestService.State.Returns(PttTestState.Idle(isSimulated: false));
        _announcementService.AnnounceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<LanguageExtError, Unit>.Success(Unit.Default));

        _handler = new StartModulationTestCommandHandler(
            _announcementService, _pttTestService, _tracker, _daemonService);
    }

    [Fact]
    public async Task ShouldAnnounceTheTestText_WhenASalonIsActive()
    {
        var result = await _handler.Handle(new StartModulationTestCommand(), CancellationToken.None);

        result.ShouldBeSuccess();
        await _announcementService.Received(1).AnnounceAsync(
            StartModulationTestCommandHandler.AnnouncementText, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShouldRefuse_WhenNoSalonIsActive()
    {
        _tracker.ActiveSalonId.Returns((Guid?)null);

        var result = await _handler.Handle(new StartModulationTestCommand(), CancellationToken.None);

        result.ShouldBeFail(errors =>
            errors.Should().Contain(error => error.Code == "MODULATION_TEST_UNAVAILABLE"));
        await _announcementService.DidNotReceive()
            .AnnounceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShouldRefuse_WhenTheDaemonIsStopped()
    {
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<LanguageExtError, bool>.Success(false));

        var result = await _handler.Handle(new StartModulationTestCommand(), CancellationToken.None);

        result.ShouldBeFail(errors =>
            errors.Should().Contain(error => error.Code == "MODULATION_TEST_UNAVAILABLE"));
        await _announcementService.DidNotReceive()
            .AnnounceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShouldRefuse_WhenACarrierTestIsInProgress()
    {
        // Les deux tests se disputeraient le PTT : le minuteur de relâchement de la porteuse
        // couperait l'annonce en pleine diffusion.
        _pttTestService.State.Returns(
            new PttTestState(true, DateTimeOffset.UtcNow.AddSeconds(5), false));

        var result = await _handler.Handle(new StartModulationTestCommand(), CancellationToken.None);

        result.ShouldBeFail(errors =>
            errors.Should().Contain(error => error.Code == "CONFLICT"));
        await _announcementService.DidNotReceive()
            .AnnounceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShouldReportTheFailure_WhenTheAnnouncementCannotBePlayed()
    {
        _announcementService.AnnounceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<LanguageExtError, Unit>.Fail(
                Prelude.Seq1(LanguageExtError.New("pico2wave est introuvable"))));

        var result = await _handler.Handle(new StartModulationTestCommand(), CancellationToken.None);

        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(error => error.Code == "MODULATION_TEST_FAILED");
            errors.Should().Contain(error => error.Message.Contains("pico2wave"));
        });
    }
}
