using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.SvxLink.RestartSvxLink;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.SvxLink;

/// <summary>
/// Tests unitaires pour RestartSvxLinkCommand et son handler (commande DTMF 320)
/// </summary>
public class RestartSvxLinkCommandTests
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;
    private readonly ISvxLinkDaemonService _daemonService;
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly IReflectorLinkStateService _linkStateService;
    private readonly ILogger<RestartSvxLinkCommandHandler> _logger;

    public RestartSvxLinkCommandTests()
    {
        _repository = Substitute.For<ISalonRepository>();
        _tracker = Substitute.For<IActiveSessionTracker>();
        _daemonService = Substitute.For<ISvxLinkDaemonService>();
        _connectedNodesService = Substitute.For<IConnectedNodesService>();
        _linkStateService = Substitute.For<IReflectorLinkStateService>();
        _logger = Substitute.For<ILogger<RestartSvxLinkCommandHandler>>();
    }

    private async Task<Validation<Error, Unit>> CallHandle() =>
        await new RestartSvxLinkCommandHandler(
                _repository, _tracker, _daemonService, _connectedNodesService, _linkStateService, _logger)
            .Handle(new RestartSvxLinkCommand(), CancellationToken.None);

    private void GivenDaemonRestartSucceeds() =>
        _daemonService.RestartAsync(Arg.Any<ReflectorProtocol>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

    [Fact]
    public async Task Handle_WithActiveReflectorSalon_ShouldBeginConnecting()
    {
        var salonId = Guid.NewGuid();
        _tracker.ActiveSalonId.Returns(salonId);
        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(CreateAggregate(salonId, ReflectorProtocol.V2).ToSuccess());
        GivenDaemonRestartSucceeds();

        var result = await CallHandle();

        result.ShouldBeSuccess();
        _linkStateService.Received(1).BeginConnecting();
        _linkStateService.DidNotReceive().MarkNotApplicable();
    }

    [Fact]
    public async Task Handle_WithoutActiveSalon_ShouldMarkLinkNotApplicable()
    {
        _tracker.ActiveSalonId.Returns((Guid?)null);
        GivenDaemonRestartSucceeds();

        var result = await CallHandle();

        result.ShouldBeSuccess();
        _linkStateService.Received(1).MarkNotApplicable();
        _linkStateService.DidNotReceive().BeginConnecting();
    }

    [Fact]
    public async Task Handle_WithActiveParrotSalon_ShouldMarkLinkNotApplicable()
    {
        var salonId = Guid.NewGuid();
        _tracker.ActiveSalonId.Returns(salonId);
        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(CreateAggregate(salonId, ReflectorProtocol.V3, SalonType.Parrot).ToSuccess());
        GivenDaemonRestartSucceeds();

        var result = await CallHandle();

        result.ShouldBeSuccess();
        _linkStateService.Received(1).MarkNotApplicable();
        _linkStateService.DidNotReceive().BeginConnecting();
    }

    [Fact]
    public async Task Handle_WithActiveSalon_ShouldRestartWithItsProtocol()
    {
        var salonId = Guid.NewGuid();
        var salon = CreateAggregate(salonId, ReflectorProtocol.V2);

        _tracker.ActiveSalonId.Returns(salonId);
        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());
        GivenDaemonRestartSucceeds();

        var result = await CallHandle();

        result.ShouldBeSuccess();
        await _daemonService.Received(1).RestartAsync(ReflectorProtocol.V2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldKeepActiveSalonUnchanged()
    {
        var salonId = Guid.NewGuid();
        var salon = CreateAggregate(salonId, ReflectorProtocol.V3);

        _tracker.ActiveSalonId.Returns(salonId);
        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>()).Returns(salon.ToSuccess());
        GivenDaemonRestartSucceeds();

        await CallHandle();

        _tracker.DidNotReceive().SetActiveSalon(Arg.Any<Guid?>());
        await _daemonService.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNoActiveSalon_ShouldRestartWithV3Protocol()
    {
        _tracker.ActiveSalonId.Returns((Guid?)null);
        GivenDaemonRestartSucceeds();

        var result = await CallHandle();

        result.ShouldBeSuccess();
        await _daemonService.Received(1).RestartAsync(ReflectorProtocol.V3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUnknownActiveSalon_ShouldFallBackToV3Protocol()
    {
        var salonId = Guid.NewGuid();

        _tracker.ActiveSalonId.Returns(salonId);
        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Salon", salonId).ToFailure<SalonAggregate>());
        GivenDaemonRestartSucceeds();

        var result = await CallHandle();

        result.ShouldBeSuccess();
        await _daemonService.Received(1).RestartAsync(ReflectorProtocol.V3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRearmConnectionAnnouncementBeforeRestart()
    {
        _tracker.ActiveSalonId.Returns((Guid?)null);
        GivenDaemonRestartSucceeds();

        await CallHandle();

        _connectedNodesService.Received(1).Reset();
    }

    [Fact]
    public async Task Handle_WhenDaemonRestartFails_ShouldReturnError()
    {
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _daemonService.RestartAsync(Arg.Any<ReflectorProtocol>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<global::LanguageExt.Common.Error, Unit>.Fail(
                Seq1(global::LanguageExt.Common.Error.New("systemctl indisponible")))));

        var result = await CallHandle();

        result.ShouldBeFail();
        result.Match(
            Succ: _ => throw new InvalidOperationException("Le résultat devrait être un échec"),
            Fail: errors => errors.Head.Code.Should().Be("SVXLINK_RESTART_ERROR"));
    }

    private static SalonAggregate CreateAggregate(
        Guid id,
        ReflectorProtocol protocol,
        SalonType salonType = SalonType.Reflector) =>
        SalonAggregate.Create(id, "Salon Test", isDefault: false, CreateValidConfiguration(protocol), salonType)
            .Match(
                Succ: a => a,
                Fail: errors => throw new InvalidOperationException($"Failed to create aggregate: {string.Join(", ", errors)}"));

    private static SvxLinkConfiguration CreateValidConfiguration(ReflectorProtocol protocol) => new(
        Guid.NewGuid(),
        Logics: "SimplexLogic,ReflectorLogic",
        CfgDir: "svxlink.d",
        CardSampleRate: 16000,
        CardChannels: 1,
        Host: "ref.f5kri.fr",
        Port: 5300,
        Callsign: "F5ABC-L",
        AuthKey: "test-auth-key-123",
        JitterBufferDelay: 0,
        ReflectorProtocol: protocol,
        CertEmail: null,
        SimplexCallsign: "F5ABC",
        Modules: "ModuleHelp,ModuleParrot",
        ShortIdentInterval: 60,
        LongIdentInterval: 60,
        ReportCtcss: "71.9",
        DefaultLang: "fr_FR",
        RgrSoundDelay: 0,
        RxFrequency: 145.550m,
        TxFrequency: 145.550m,
        RxCtcss: 136.5m,
        TxCtcss: 136.5m);
}
