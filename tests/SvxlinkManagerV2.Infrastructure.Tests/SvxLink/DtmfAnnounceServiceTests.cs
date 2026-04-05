using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

/// <summary>
/// Tests unitaires pour DtmfAnnounceService
/// </summary>
public class DtmfAnnounceServiceTests
{
    private readonly IDtmfCommandTracker _dtmfTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _scopedProvider;
    private readonly IMediator _mediator;
    private readonly ILogger<DtmfAnnounceService> _logger;

    private Action<string>? _capturedHandler;

    public DtmfAnnounceServiceTests()
    {
        _dtmfTracker = Substitute.For<IDtmfCommandTracker>();
        _mediator = Substitute.For<IMediator>();
        _logger = Substitute.For<ILogger<DtmfAnnounceService>>();

        _scopedProvider = Substitute.For<IServiceProvider>();
        _scopedProvider.GetService(typeof(IMediator)).Returns(_mediator);

        _scope = Substitute.For<IServiceScope>();
        _scope.ServiceProvider.Returns(_scopedProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(_scope);

        // Capturer le handler abonné à l'événement
        _dtmfTracker.OnDtmfCommandReceived += Arg.Do<Action<string>>(h => _capturedHandler = h);
    }

    private DtmfAnnounceService CreateService() =>
        new(_dtmfTracker, _scopeFactory, _logger);

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
    // Filtrage des plages
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1")]
    [InlineData("96")]
    [InlineData("299")]
    [InlineData("400")]
    [InlineData("9999")]
    public async Task Command_OutsideInfoRange_ShouldBeIgnored(string rawCommand)
    {
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _capturedHandler!.Invoke(rawCommand);
        await Task.Delay(50); // laisser le async void se terminer

        await _mediator.DidNotReceive().Send(Arg.Any<IRequest<SalonAggregate?>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Command_NonNumeric_ShouldBeIgnored(string rawCommand)
    {
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _capturedHandler!.Invoke(rawCommand);
        await Task.Delay(50);

        await _mediator.DidNotReceive().Send(Arg.Any<IRequest<SalonAggregate?>>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Commande 300 — Annonce salon actif
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Command300_WithActiveSalon_ShouldQueryActiveSalonAndLog()
    {
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(salonId, "Salon National France", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        _mediator.Send(Arg.Any<GetActiveSalonQuery>(), Arg.Any<CancellationToken>())
                 .Returns(Task.FromResult<SalonAggregate?>(salon));

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _capturedHandler!.Invoke("300");
        await Task.Delay(50);

        await _mediator.Received(1).Send(Arg.Any<GetActiveSalonQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command300_WithNoActiveSalon_ShouldNotThrow()
    {
        _mediator.Send(Arg.Any<GetActiveSalonQuery>(), Arg.Any<CancellationToken>())
                 .Returns(Task.FromResult<SalonAggregate?>(null));

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        var act = async () =>
        {
            _capturedHandler!.Invoke("300");
            await Task.Delay(50);
        };

        await act.Should().NotThrowAsync();
        await _mediator.Received(1).Send(Arg.Any<GetActiveSalonQuery>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Commandes non mappées dans la plage 300-399
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("301")]
    [InlineData("350")]
    [InlineData("399")]
    public async Task Command_UnmappedInfoRange_ShouldNotCallMediator(string rawCommand)
    {
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _capturedHandler!.Invoke(rawCommand);
        await Task.Delay(50);

        await _mediator.DidNotReceive().Send(Arg.Any<IRequest<SalonAggregate?>>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Constantes de plage exportées
    // -------------------------------------------------------------------------

    [Fact]
    public void RangeConstants_ShouldBeCorrect()
    {
        DtmfAnnounceService.RangeMin.Should().Be(300);
        DtmfAnnounceService.RangeMax.Should().Be(399);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SvxLinkConfiguration CreateValidConfiguration() =>
        new(Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d", 16000, 1,
            "ref.f5kri.fr", 5300,
            "F5ABC-L", "test-auth-key-123", 0,
            "F5ABC", "ModuleHelp,ModuleParrot", 60, 60,
            "71.9", "fr_FR", 0,
            145.550m, 145.550m, 136.5m, 136.5m);
}
