using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.ActivateParrot;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour ActivateParrotCommand et son handler.
/// Le handler orchestre : arrêt du daemon si actif → génération config Parrot → restart daemon → activation DTMF.
/// Note : le SA818 n'est PAS reconfiguré en mode Perroquet.
/// </summary>
public class ActivateParrotCommandTests
{
    private readonly IActiveSessionTracker _tracker;
    private readonly ISvxLinkParrotConfigurationService _parrotConfigurationService;
    private readonly ISvxLinkDaemonService _daemonService;
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly ILogger<ActivateParrotCommandHandler> _logger;

    public ActivateParrotCommandTests()
    {
        _tracker = Substitute.For<IActiveSessionTracker>();
        _parrotConfigurationService = Substitute.For<ISvxLinkParrotConfigurationService>();
        _daemonService = Substitute.For<ISvxLinkDaemonService>();
        _connectedNodesService = Substitute.For<IConnectedNodesService>();
        _logger = Substitute.For<ILogger<ActivateParrotCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WhenNothingIsActive_ShouldActivateParrotAndUpdateTracker()
    {
        // Arrange
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _tracker.IsParrotActive.Returns(false);
        _parrotConfigurationService.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.SendDtmfCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle();

        // Assert
        result.ShouldBeSuccess();
        _tracker.Received(1).SetParrotActive(true);
        await _daemonService.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNothingIsActive_ShouldSendDtmfCommandToActivateModule()
    {
        // Arrange
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _tracker.IsParrotActive.Returns(false);
        _parrotConfigurationService.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.SendDtmfCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle();

        // Assert — DTMF "2#" envoyé pour activer ModuleParrot (ID=2 dans svxlink.conf)
        result.ShouldBeSuccess();
        await _daemonService.Received(1).SendDtmfCommandAsync("2#", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSalonIsActive_ShouldStopDaemonFirst()
    {
        // Arrange
        var activeSalonId = Guid.NewGuid();
        _tracker.ActiveSalonId.Returns((Guid?)activeSalonId);
        _tracker.IsParrotActive.Returns(false);
        _daemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _parrotConfigurationService.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.SendDtmfCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle();

        // Assert
        result.ShouldBeSuccess();
        await _daemonService.Received(1).StopAsync(Arg.Any<CancellationToken>());
        _connectedNodesService.Received(1).Reset();
        _tracker.Received(1).SetActiveSalon(null);
        _tracker.Received(1).SetParrotActive(false);
        _tracker.Received(1).SetParrotActive(true);
    }

    [Fact]
    public async Task Handle_WhenParrotIsAlreadyActive_ShouldStopDaemonFirst()
    {
        // Arrange
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _tracker.IsParrotActive.Returns(true);
        _daemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _parrotConfigurationService.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.SendDtmfCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle();

        // Assert
        result.ShouldBeSuccess();
        await _daemonService.Received(1).StopAsync(Arg.Any<CancellationToken>());
        _tracker.Received(1).SetParrotActive(false);
        _tracker.Received(1).SetParrotActive(true);
    }

    [Fact]
    public async Task Handle_WhenDaemonStopFails_ShouldFail()
    {
        // Arrange
        var activeSalonId = Guid.NewGuid();
        _tracker.ActiveSalonId.Returns((Guid?)activeSalonId);
        _tracker.IsParrotActive.Returns(false);
        _daemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(
                global::LanguageExt.Common.Error.New("SVXLINK_STOP_ERROR")));

        // Act
        var result = await CallHandle();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SVXLINK_STOP_ERROR");
        });
        _tracker.DidNotReceive().SetParrotActive(true);
        await _parrotConfigurationService.DidNotReceive().GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenConfigGenerationFails_ShouldFail()
    {
        // Arrange
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _tracker.IsParrotActive.Returns(false);
        _parrotConfigurationService.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(
                global::LanguageExt.Common.Error.New("CONFIG_ERROR")));

        // Act
        var result = await CallHandle();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SVXLINK_CONFIG_ERROR");
        });
        _tracker.DidNotReceive().SetParrotActive(true);
        await _daemonService.DidNotReceive().RestartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDaemonRestartFails_ShouldFail()
    {
        // Arrange
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _tracker.IsParrotActive.Returns(false);
        _parrotConfigurationService.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(
                global::LanguageExt.Common.Error.New("SVXLINK_RESTART_ERROR")));

        // Act
        var result = await CallHandle();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SVXLINK_RESTART_ERROR");
        });
        _tracker.DidNotReceive().SetParrotActive(true);
        await _daemonService.DidNotReceive().SendDtmfCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDtmfFails_ShouldStillSucceed()
    {
        // Arrange — l'échec DTMF est non-bloquant (warning) : le module peut être activé manuellement
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _tracker.IsParrotActive.Returns(false);
        _parrotConfigurationService.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.SendDtmfCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(
                global::LanguageExt.Common.Error.New("DTMF_ERROR")));

        // Act
        var result = await CallHandle();

        // Assert — succès malgré l'échec DTMF
        result.ShouldBeSuccess();
        _tracker.Received(1).SetParrotActive(true);
    }

    [Fact]
    public async Task Handle_ShouldNotReconfigureSA818()
    {
        // Arrange — vérifier qu'aucun service SA818 n'est injecté ni appelé
        // (le handler ne dépend pas de ISA818Service ni ISA818Repository)
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _tracker.IsParrotActive.Returns(false);
        _parrotConfigurationService.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.SendDtmfCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle();

        // Assert
        result.ShouldBeSuccess();
        // Le handler ActivateParrotCommandHandler ne dépend pas du SA818 — vérifié structurellement
        // par l'absence de ISA818Service et ISA818Repository dans ses dépendances
    }

    private Task<Validation<Error, Unit>> CallHandle()
    {
        var handler = new ActivateParrotCommandHandler(
            _tracker,
            _parrotConfigurationService,
            _daemonService,
            _connectedNodesService,
            _logger);
        return handler.Handle(new ActivateParrotCommand(), CancellationToken.None);
    }
}
