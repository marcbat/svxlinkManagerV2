using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Audio.StartPttTest;
using SvxlinkManagerV2.Application.Features.Audio.StopPttTest;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;
using LanguageExtError = LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Application.Tests.Features.Audio;

/// <summary>
/// Tests unitaires des commandes de test d'émission.
/// </summary>
public class PttTestCommandHandlerTests
{
    private readonly IPttTestService _pttTestService = Substitute.For<IPttTestService>();
    private readonly IActiveSessionTracker _tracker = Substitute.For<IActiveSessionTracker>();
    private readonly ISvxLinkDaemonService _daemonService = Substitute.For<ISvxLinkDaemonService>();

    private readonly StartPttTestCommandHandler _startHandler;
    private readonly StopPttTestCommandHandler _stopHandler;

    public PttTestCommandHandlerTests()
    {
        _tracker.ActiveSalonId.Returns(Guid.NewGuid());
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>()).Returns(DaemonRunning(true));

        _pttTestService.StartAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PttTestState(true, DateTimeOffset.UtcNow.AddSeconds(5), false).ToSuccess());
        _pttTestService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(PttTestState.Idle(isSimulated: false).ToSuccess());

        _startHandler = new StartPttTestCommandHandler(_pttTestService, _tracker, _daemonService);
        _stopHandler = new StopPttTestCommandHandler(_pttTestService, _tracker, _daemonService);
    }

    [Fact]
    public async Task Start_ShouldKeyThePtt_WhenASalonIsActive()
    {
        var result = await _startHandler.Handle(new StartPttTestCommand(5), CancellationToken.None);

        result.ShouldBeSuccess(status => status.IsTransmitting.Should().BeTrue());
        await _pttTestService.Received(1).StartAsync(5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_ShouldRefuse_WhenNoSalonIsActive()
    {
        _tracker.ActiveSalonId.Returns((Guid?)null);

        var result = await _startHandler.Handle(new StartPttTestCommand(5), CancellationToken.None);

        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(error => error.Code == "PTT_TEST_UNAVAILABLE");
            errors.Should().Contain(error => error.Message.Contains("Aucun salon"));
        });
        await _pttTestService.DidNotReceive().StartAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_ShouldRefuse_WhenTheDaemonIsStopped()
    {
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>()).Returns(DaemonRunning(false));

        var result = await _startHandler.Handle(new StartPttTestCommand(5), CancellationToken.None);

        result.ShouldBeFail(errors => errors.Should().Contain(error => error.Code == "PTT_TEST_UNAVAILABLE"));
        await _pttTestService.DidNotReceive().StartAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_ShouldRefuse_WhenTheDaemonStateIsUnknown()
    {
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<LanguageExtError, bool>.Fail(
                Prelude.Seq1(LanguageExtError.New(500, "état inconnu"))));

        var result = await _startHandler.Handle(new StartPttTestCommand(5), CancellationToken.None);

        result.ShouldBeFail();
        await _pttTestService.DidNotReceive().StartAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_ShouldPropagateTheServiceRefusal()
    {
        _pttTestService.StartAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Fail<PttTestState>("PTT_TEST_DURATION_TOO_LONG", "durée trop longue"));

        var result = await _startHandler.Handle(new StartPttTestCommand(120), CancellationToken.None);

        result.ShouldBeFail(errors =>
            errors.Should().Contain(error => error.Code == "PTT_TEST_DURATION_TOO_LONG"));
    }

    [Fact]
    public async Task Stop_ShouldReleaseThePtt()
    {
        var result = await _stopHandler.Handle(new StopPttTestCommand(), CancellationToken.None);

        result.ShouldBeSuccess(status => status.IsTransmitting.Should().BeFalse());
        await _pttTestService.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_ShouldReleaseThePtt_EvenWhenTheDaemonHasStopped()
    {
        // Relâcher doit rester possible en toute circonstance : c'est l'action de repli.
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>()).Returns(DaemonRunning(false));

        var result = await _stopHandler.Handle(new StopPttTestCommand(), CancellationToken.None);

        result.ShouldBeSuccess();
        await _pttTestService.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// L'état du daemon transite par le type d'erreur de LanguageExt, non par celui du domaine.
    /// </summary>
    private static Validation<LanguageExtError, bool> DaemonRunning(bool running) =>
        Validation<LanguageExtError, bool>.Success(running);

    private static Validation<Error, T> Fail<T>(string code, string message)
        => Validation<Error, T>.Fail(Prelude.Seq1(Error.Validation(code, message)));
}
