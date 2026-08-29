using LanguageExt;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;
using SvxlinkManagerV2.Application.Features.Salons.ActivateStandaloneMode;
using SvxlinkManagerV2.Application.Features.Salons.GetAdjacentSalon;
using SvxlinkManagerV2.Application.Features.Salons.GetDefaultSalon;
using SvxlinkManagerV2.Application.Features.SvxLink.RestartSvxLink;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;
using Unit = LanguageExt.Unit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests unitaires pour DtmfSystemCommandService (routage des commandes DTMF système 310-320)
/// </summary>
public class DtmfSystemCommandServiceTests
{
    private readonly IDtmfCommandTracker _dtmfTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMediator _mediator;
    private readonly IVoiceAnnouncementService _announcer;
    private readonly IActiveSessionTracker _sessionTracker;
    private readonly ILogger<DtmfSystemCommandService> _logger;

    private Action<string>? _capturedHandler;

    public DtmfSystemCommandServiceTests()
    {
        _dtmfTracker = Substitute.For<IDtmfCommandTracker>();
        _mediator = Substitute.For<IMediator>();
        _announcer = Substitute.For<IVoiceAnnouncementService>();
        _sessionTracker = Substitute.For<IActiveSessionTracker>();
        _logger = Substitute.For<ILogger<DtmfSystemCommandService>>();

        var scopedProvider = Substitute.For<IServiceProvider>();
        scopedProvider.GetService(typeof(IMediator)).Returns(_mediator);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(scopedProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        _announcer.AnnounceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<global::LanguageExt.Common.Error, Unit>.Success(Unit.Default));

        // Capturer le handler abonné à l'événement
        _dtmfTracker.OnDtmfCommandReceived += Arg.Do<Action<string>>(h => _capturedHandler = h);
    }

    private DtmfSystemCommandService CreateService() =>
        new(_dtmfTracker, _scopeFactory, _announcer, _sessionTracker, _logger);

    /// <summary>Démarre le service, envoie la commande et laisse le handler async void se terminer.</summary>
    private async Task<DtmfSystemCommandService> DispatchAsync(string rawCommand)
    {
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _capturedHandler!.Invoke(rawCommand);
        await Task.Delay(100);

        return service;
    }

    private void GivenActivationSucceeds() =>
        _mediator.Send(Arg.Any<ActivateSalonCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<Error, Unit>>(unit.ToSuccess()));

    // -------------------------------------------------------------------------
    // Abonnement / désabonnement
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_ShouldSubscribeToCommandTracker()
    {
        var service = CreateService();

        await service.StartAsync(CancellationToken.None);

        _dtmfTracker.Received(1).OnDtmfCommandReceived += Arg.Any<Action<string>>();
    }

    [Fact]
    public async Task StopAsync_ShouldUnsubscribeFromCommandTracker()
    {
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        await service.StopAsync(CancellationToken.None);

        _dtmfTracker.Received(1).OnDtmfCommandReceived -= Arg.Any<Action<string>>();
    }

    // -------------------------------------------------------------------------
    // Filtrage des codes non système
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1")]
    [InlineData("96")]
    [InlineData("300")]
    [InlineData("301")]
    [InlineData("314")]
    [InlineData("321")]
    [InlineData("399")]
    [InlineData("9999")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task Command_NotASystemCode_ShouldBeIgnored(string rawCommand)
    {
        await DispatchAsync(rawCommand);

        _scopeFactory.DidNotReceive().CreateScope();
        await _announcer.DidNotReceive().AnnounceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Commande 310 — Retour au salon par défaut
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Command310_ShouldActivateDefaultSalonAndAnnounce()
    {
        var defaultSalon = CreateAggregate("Salon National");
        _mediator.Send(Arg.Any<GetDefaultSalonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SalonAggregate?>(defaultSalon));
        _sessionTracker.IsSalonActive(defaultSalon.Id).Returns(false);
        GivenActivationSucceeds();

        await DispatchAsync("310");

        await _mediator.Received(1).Send(
            Arg.Is<ActivateSalonCommand>(c => c.Id == defaultSalon.Id), Arg.Any<CancellationToken>());
        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("Salon National")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command310_WhenDefaultSalonAlreadyActive_ShouldAnnounceWithoutActivating()
    {
        var defaultSalon = CreateAggregate("Salon National");
        _mediator.Send(Arg.Any<GetDefaultSalonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SalonAggregate?>(defaultSalon));
        _sessionTracker.IsSalonActive(defaultSalon.Id).Returns(true);

        await DispatchAsync("310");

        await _mediator.DidNotReceive().Send(Arg.Any<ActivateSalonCommand>(), Arg.Any<CancellationToken>());
        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("déjà actif")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command310_WithNoDefaultSalon_ShouldAnnounceWithoutActivating()
    {
        _mediator.Send(Arg.Any<GetDefaultSalonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SalonAggregate?>(null));

        await DispatchAsync("310");

        await _mediator.DidNotReceive().Send(Arg.Any<ActivateSalonCommand>(), Arg.Any<CancellationToken>());
        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("Aucun salon par défaut")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command310_WhenActivationFails_ShouldAnnounceFailure()
    {
        var defaultSalon = CreateAggregate("Salon National");
        _mediator.Send(Arg.Any<GetDefaultSalonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SalonAggregate?>(defaultSalon));
        _sessionTracker.IsSalonActive(defaultSalon.Id).Returns(false);
        _mediator.Send(Arg.Any<ActivateSalonCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Error.Validation("SVXLINK_RESTART_ERROR", "boum").ToFailure<Unit>()));

        await DispatchAsync("310");

        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("Échec")), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Commande 311 — Déconnexion (mode autonome)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Command311_WithActiveSalon_ShouldActivateStandaloneModeAndAnnounce()
    {
        _sessionTracker.ActiveSalonId.Returns(Guid.NewGuid());
        _mediator.Send(Arg.Any<ActivateStandaloneModeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<Error, Unit>>(unit.ToSuccess()));

        await DispatchAsync("311");

        await _mediator.Received(1).Send(Arg.Any<ActivateStandaloneModeCommand>(), Arg.Any<CancellationToken>());
        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("mode autonome")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command311_WithNoActiveSalon_ShouldAnnounceWithoutSwitchingMode()
    {
        _sessionTracker.ActiveSalonId.Returns((Guid?)null);

        await DispatchAsync("311");

        await _mediator.DidNotReceive().Send(Arg.Any<ActivateStandaloneModeCommand>(), Arg.Any<CancellationToken>());
        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("déjà en mode autonome")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command311_WhenStandaloneModeFails_ShouldAnnounceFailure()
    {
        _sessionTracker.ActiveSalonId.Returns(Guid.NewGuid());
        _mediator.Send(Arg.Any<ActivateStandaloneModeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Error.Validation("SVXLINK_RESTART_ERROR", "boum").ToFailure<Unit>()));

        await DispatchAsync("311");

        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("Échec")), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Commandes 312 / 313 — Navigation entre salons
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("312", SalonNavigationDirection.Next)]
    [InlineData("313", SalonNavigationDirection.Previous)]
    public async Task NavigationCommand_ShouldQueryTheExpectedDirectionAndActivate(
        string rawCommand, SalonNavigationDirection expectedDirection)
    {
        var target = CreateAggregate("Salon Suivant");
        _mediator.Send(Arg.Any<GetAdjacentSalonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SalonAggregate?>(target));
        _sessionTracker.IsSalonActive(target.Id).Returns(false);
        GivenActivationSucceeds();

        await DispatchAsync(rawCommand);

        await _mediator.Received(1).Send(
            Arg.Is<GetAdjacentSalonQuery>(q => q.Direction == expectedDirection), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(
            Arg.Is<ActivateSalonCommand>(c => c.Id == target.Id), Arg.Any<CancellationToken>());
        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("Salon Suivant")), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("312")]
    [InlineData("313")]
    public async Task NavigationCommand_WithNoNavigableSalon_ShouldAnnounceWithoutActivating(string rawCommand)
    {
        _mediator.Send(Arg.Any<GetAdjacentSalonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SalonAggregate?>(null));

        await DispatchAsync(rawCommand);

        await _mediator.DidNotReceive().Send(Arg.Any<ActivateSalonCommand>(), Arg.Any<CancellationToken>());
        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("code DTMF")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NavigationCommand_WhenTargetIsAlreadyActive_ShouldAnnounceWithoutActivating()
    {
        // Cas d'un unique salon navigable : la rotation retombe sur le salon actif.
        var target = CreateAggregate("Salon Unique");
        _mediator.Send(Arg.Any<GetAdjacentSalonQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SalonAggregate?>(target));
        _sessionTracker.IsSalonActive(target.Id).Returns(true);

        await DispatchAsync("312");

        await _mediator.DidNotReceive().Send(Arg.Any<ActivateSalonCommand>(), Arg.Any<CancellationToken>());
        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("déjà actif")), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Commande 320 — Redémarrage du daemon
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Command320_ShouldRestartDaemonAndAnnounce()
    {
        _mediator.Send(Arg.Any<RestartSvxLinkCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<Error, Unit>>(unit.ToSuccess()));

        await DispatchAsync("320");

        await _mediator.Received(1).Send(Arg.Any<RestartSvxLinkCommand>(), Arg.Any<CancellationToken>());
        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("redémarré")), Arg.Any<CancellationToken>());

        // Le salon actif est conservé : aucune commande d'activation n'est émise.
        await _mediator.DidNotReceive().Send(Arg.Any<ActivateSalonCommand>(), Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(Arg.Any<ActivateStandaloneModeCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command320_WhenRestartFails_ShouldAnnounceFailure()
    {
        _mediator.Send(Arg.Any<RestartSvxLinkCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Error.Validation("SVXLINK_RESTART_ERROR", "boum").ToFailure<Unit>()));

        await DispatchAsync("320");

        await _announcer.Received(1).AnnounceAsync(
            Arg.Is<string>(t => t.Contains("Échec")), Arg.Any<CancellationToken>());
    }

    private static SalonAggregate CreateAggregate(string name) =>
        SalonAggregate.Create(Guid.NewGuid(), name, isDefault: false, CreateValidConfiguration())
            .Match(
                Succ: a => a,
                Fail: errors => throw new InvalidOperationException($"Failed to create aggregate: {string.Join(", ", errors)}"));

    private static SvxLinkConfiguration CreateValidConfiguration() => new(
        Guid.NewGuid(),
        "SimplexLogic,ReflectorLogic", "svxlink.d", 16000, 1,
        "ref.f5kri.fr", 5300, "F5ABC-L", "test-auth-key-123", 0,
        ReflectorProtocol.V2, null,
        "F5ABC", "ModuleHelp", 60, 60,
        null, "fr_FR", 0,
        145.550m, 145.550m, null, null);
}
