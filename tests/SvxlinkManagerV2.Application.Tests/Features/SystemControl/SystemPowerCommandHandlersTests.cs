using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.SystemControl;
using SvxlinkManagerV2.Application.Features.SystemControl.GetSystemControlAvailability;
using SvxlinkManagerV2.Application.Features.SystemControl.RebootSystem;
using SvxlinkManagerV2.Application.Features.SystemControl.ShutdownSystem;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using DaemonError = LanguageExt.Common.Error;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Tests.Features.SystemControl;

/// <summary>
/// Tests unitaires des commandes d'alimentation (redémarrage / arrêt de la machine).
/// </summary>
public class SystemPowerCommandHandlersTests
{
    private readonly ISystemControlService _systemControlService;
    private readonly ISvxLinkDaemonService _svxLinkDaemonService;
    private readonly IReflectorDaemonService _reflectorDaemonService;

    public SystemPowerCommandHandlersTests()
    {
        _systemControlService = Substitute.For<ISystemControlService>();
        _svxLinkDaemonService = Substitute.For<ISvxLinkDaemonService>();
        _reflectorDaemonService = Substitute.For<IReflectorDaemonService>();

        _svxLinkDaemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<DaemonError, Unit>.Success(Unit.Default)));
        _reflectorDaemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<DaemonError, Unit>.Success(Unit.Default)));
    }

    [Fact]
    public async Task RebootHandler_ShouldStopDaemonsBeforeTriggeringReboot()
    {
        GivenPlatformSupported();
        _systemControlService.RebootAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<Error, Unit>.Success(Unit.Default)));

        var result = await CreateRebootHandler().Handle(new RebootSystemCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _svxLinkDaemonService.StopAsync(Arg.Any<CancellationToken>());
            _reflectorDaemonService.StopAsync(Arg.Any<CancellationToken>());
            _systemControlService.RebootAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task ShutdownHandler_ShouldStopDaemonsBeforeTriggeringShutdown()
    {
        GivenPlatformSupported();
        _systemControlService.ShutdownAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<Error, Unit>.Success(Unit.Default)));

        var result = await CreateShutdownHandler().Handle(new ShutdownSystemCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _svxLinkDaemonService.StopAsync(Arg.Any<CancellationToken>());
            _reflectorDaemonService.StopAsync(Arg.Any<CancellationToken>());
            _systemControlService.ShutdownAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task RebootHandler_ShouldFailAndKeepDaemonsRunning_WhenPlatformIsNotSupported()
    {
        GivenPlatformUnsupported("L'application s'exécute dans un conteneur.");

        var result = await CreateRebootHandler().Handle(new RebootSystemCommand(), CancellationToken.None);

        result.IsFail.Should().BeTrue();
        result.Match(
            Succ: _ => Assert.Fail("Un échec était attendu"),
            Fail: errors =>
            {
                errors.Should().ContainSingle();
                errors.Head.Code.Should().Be("SYSTEM_CONTROL_UNSUPPORTED");
                errors.Head.Message.Should().Be("L'application s'exécute dans un conteneur.");
            });

        await _systemControlService.DidNotReceive().RebootAsync(Arg.Any<CancellationToken>());
        await _svxLinkDaemonService.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
        await _reflectorDaemonService.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShutdownHandler_ShouldFailAndKeepDaemonsRunning_WhenPlatformIsNotSupported()
    {
        GivenPlatformUnsupported("Le contrôle de l'alimentation est désactivé.");

        var result = await CreateShutdownHandler().Handle(new ShutdownSystemCommand(), CancellationToken.None);

        result.IsFail.Should().BeTrue();
        await _systemControlService.DidNotReceive().ShutdownAsync(Arg.Any<CancellationToken>());
        await _svxLinkDaemonService.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
        await _reflectorDaemonService.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RebootHandler_ShouldStillTriggerReboot_WhenDaemonStopFails()
    {
        GivenPlatformSupported();
        _svxLinkDaemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<DaemonError, Unit>.Fail(
                LanguageExt.Prelude.Seq1(DaemonError.New("systemctl indisponible")))));
        _reflectorDaemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Validation<DaemonError, Unit>>>(_ => throw new InvalidOperationException("boom"));
        _systemControlService.RebootAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<Error, Unit>.Success(Unit.Default)));

        var result = await CreateRebootHandler().Handle(new RebootSystemCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _systemControlService.Received(1).RebootAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RebootHandler_ShouldPropagateSystemControlFailure()
    {
        GivenPlatformSupported();
        _systemControlService.RebootAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Error.Validation("SYSTEM_CONTROL_REBOOT_ERROR", "Permission refusée.").ToFailure<Unit>()));

        var result = await CreateRebootHandler().Handle(new RebootSystemCommand(), CancellationToken.None);

        result.IsFail.Should().BeTrue();
        result.Match(
            Succ: _ => Assert.Fail("Un échec était attendu"),
            Fail: errors => errors.Head.Code.Should().Be("SYSTEM_CONTROL_REBOOT_ERROR"));
    }

    [Fact]
    public async Task GetAvailabilityHandler_ShouldReturnServiceAvailability()
    {
        var expected = new SystemControlAvailabilityDto(false, false, "Plateforme non supportée.");
        _systemControlService.GetAvailability().Returns(expected);

        var handler = new GetSystemControlAvailabilityQueryHandler(_systemControlService);
        var result = await handler.Handle(new GetSystemControlAvailabilityQuery(), CancellationToken.None);

        result.Should().Be(expected);
    }

    private void GivenPlatformSupported()
        => _systemControlService.GetAvailability()
            .Returns(new SystemControlAvailabilityDto(true, false, null));

    private void GivenPlatformUnsupported(string reason)
        => _systemControlService.GetAvailability()
            .Returns(new SystemControlAvailabilityDto(false, false, reason));

    private RebootSystemCommandHandler CreateRebootHandler()
        => new(
            _systemControlService,
            _svxLinkDaemonService,
            _reflectorDaemonService,
            Substitute.For<ILogger<RebootSystemCommandHandler>>());

    private ShutdownSystemCommandHandler CreateShutdownHandler()
        => new(
            _systemControlService,
            _svxLinkDaemonService,
            _reflectorDaemonService,
            Substitute.For<ILogger<ShutdownSystemCommandHandler>>());
}
