using FluentAssertions;
using LanguageExt;
using LanguageExt.Common;
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
using static LanguageExt.Prelude;

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
    private readonly IEnumerable<IInfoProvider> _infoProviders;
    private readonly ITtsService _ttsService;
    private readonly IDtmfPtyWriter _ptyWriter;
    private readonly ILogger<DtmfAnnounceService> _logger;

    private Action<string>? _capturedHandler;

    public DtmfAnnounceServiceTests()
    {
        _dtmfTracker = Substitute.For<IDtmfCommandTracker>();
        _mediator = Substitute.For<IMediator>();
        _logger = Substitute.For<ILogger<DtmfAnnounceService>>();
        _ttsService = Substitute.For<ITtsService>();
        _ptyWriter = Substitute.For<IDtmfPtyWriter>();

        _scopedProvider = Substitute.For<IServiceProvider>();
        _scopedProvider.GetService(typeof(IMediator)).Returns(_mediator);

        _scope = Substitute.For<IServiceScope>();
        _scope.ServiceProvider.Returns(_scopedProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(_scope);

        _infoProviders = Enumerable.Empty<IInfoProvider>();

        // Capturer le handler abonné à l'événement
        _dtmfTracker.OnDtmfCommandReceived += Arg.Do<Action<string>>(h => _capturedHandler = h);
    }

    private DtmfAnnounceService CreateService() =>
        new(_dtmfTracker, _scopeFactory, _infoProviders, _ttsService, _ptyWriter, _logger);

    private DtmfAnnounceService CreateServiceWithProviders(params IInfoProvider[] providers) =>
        new(_dtmfTracker, _scopeFactory, providers, _ttsService, _ptyWriter, _logger);

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
    // Commande 300 — Annonce contextuelle du salon actif
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Command300_WithActiveSalon_ShouldGenerateTtsAndSendToPty()
    {
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(salonId, "Salon National France", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        _mediator.Send(Arg.Any<GetActiveSalonQuery>(), Arg.Any<CancellationToken>())
                 .Returns(Task.FromResult<SalonAggregate?>(salon));
        _ttsService.GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns(Validation<Error, string>.Success(DtmfAnnounceService.TtsWavPath));
        _ptyWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(Validation<Error, LanguageExt.Unit>.Success(LanguageExt.Unit.Default));

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _capturedHandler!.Invoke("300");
        await Task.Delay(100);

        await _mediator.Received(1).Send(Arg.Any<GetActiveSalonQuery>(), Arg.Any<CancellationToken>());
        await _ttsService.Received(1).GenerateWavAsync(
            Arg.Is<string>(t => t.Contains("F5ABC") && t.Contains("Salon National France")),
            DtmfAnnounceService.TtsWavPath,
            Arg.Any<CancellationToken>());
        await _ptyWriter.Received(1).SendCommandAsync(
            DtmfAnnounceService.TtsInternalCode.ToString(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command300_WithNoActiveSalon_ShouldNotCallTts()
    {
        _mediator.Send(Arg.Any<GetActiveSalonQuery>(), Arg.Any<CancellationToken>())
                 .Returns(Task.FromResult<SalonAggregate?>(null));

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _capturedHandler!.Invoke("300");
        await Task.Delay(50);

        await _mediator.Received(1).Send(Arg.Any<GetActiveSalonQuery>(), Arg.Any<CancellationToken>());
        await _ttsService.DidNotReceive().GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _ptyWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command300_WhenTtsFails_ShouldNotSendToPty()
    {
        var salonId = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var salon = SalonAggregate.Create(salonId, "Salon Test", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        _mediator.Send(Arg.Any<GetActiveSalonQuery>(), Arg.Any<CancellationToken>())
                 .Returns(Task.FromResult<SalonAggregate?>(salon));
        _ttsService.GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns(Validation<Error, string>.Fail(Seq1(Error.New("pico2wave introuvable"))));

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _capturedHandler!.Invoke("300");
        await Task.Delay(100);

        await _ptyWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Commande 399 — Interne, doit être ignorée
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Command399_ShouldBeIgnored_AsInternalCommand()
    {
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _capturedHandler!.Invoke("399");
        await Task.Delay(50);

        await _mediator.DidNotReceive().Send(Arg.Any<IRequest<SalonAggregate?>>(), Arg.Any<CancellationToken>());
        await _ttsService.DidNotReceive().GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _ptyWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Commandes 301-398 — Dispatch vers IInfoProvider
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Command301_WithMatchingProvider_ShouldCallProviderTtsAndPty()
    {
        // Arrange
        var provider = Substitute.For<IInfoProvider>();
        provider.DtmfCode.Returns(301);
        provider.Description.Returns("Température CPU");
        provider.GetInfoTextAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, string>.Success("La température est de 42 degrés"));

        _ttsService.GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, string>.Success(DtmfAnnounceService.TtsWavPath));

        _ptyWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, LanguageExt.Unit>.Success(LanguageExt.Unit.Default));

        var service = CreateServiceWithProviders(provider);
        await service.StartAsync(CancellationToken.None);

        // Act
        _capturedHandler!.Invoke("301");
        await Task.Delay(100);

        // Assert
        await provider.Received(1).GetInfoTextAsync(Arg.Any<CancellationToken>());
        await _ttsService.Received(1).GenerateWavAsync(
            "La température est de 42 degrés",
            DtmfAnnounceService.TtsWavPath,
            Arg.Any<CancellationToken>());
        await _ptyWriter.Received(1).SendCommandAsync(
            DtmfAnnounceService.TtsInternalCode.ToString(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command301_WhenProviderFails_ShouldNotCallTts()
    {
        // Arrange
        var provider = Substitute.For<IInfoProvider>();
        provider.DtmfCode.Returns(301);
        provider.GetInfoTextAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, string>.Fail(Seq1(Error.New("Erreur lecture température"))));

        var service = CreateServiceWithProviders(provider);
        await service.StartAsync(CancellationToken.None);

        // Act
        _capturedHandler!.Invoke("301");
        await Task.Delay(100);

        // Assert : TTS ne doit pas être appelé si le provider échoue
        await _ttsService.DidNotReceive().GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _ptyWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command301_WhenTtsFails_ShouldNotCallPty()
    {
        // Arrange
        var provider = Substitute.For<IInfoProvider>();
        provider.DtmfCode.Returns(301);
        provider.GetInfoTextAsync(Arg.Any<CancellationToken>())
            .Returns(Validation<Error, string>.Success("Texte valide"));

        _ttsService.GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Validation<Error, string>.Fail(Seq1(Error.New("pico2wave introuvable"))));

        var service = CreateServiceWithProviders(provider);
        await service.StartAsync(CancellationToken.None);

        // Act
        _capturedHandler!.Invoke("301");
        await Task.Delay(100);

        // Assert : PTY ne doit pas être écrit si le TTS échoue
        await _ptyWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("302")]
    [InlineData("350")]
    [InlineData("398")]
    public async Task Command_InfoRange_WithNoMatchingProvider_ShouldNotCallTts(string rawCommand)
    {
        // Aucun provider enregistré → la commande est ignorée silencieusement
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _capturedHandler!.Invoke(rawCommand);
        await Task.Delay(50);

        await _ttsService.DidNotReceive().GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _ptyWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
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
        DtmfAnnounceService.TtsInternalCode.Should().Be(399);
        DtmfAnnounceService.TtsWavPath.Should().Be("/tmp/svxlink_tts.wav");
    }

    // -------------------------------------------------------------------------
    // BuildAnnounceText — construction du texte TTS DTMF 300
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildAnnounceText_SimplexMode_ShouldNotIncludeFrequency()
    {
        // RxFrequency == TxFrequency → pas d'annonce de fréquence
        var config = CreateValidConfiguration(); // 145.550 / 145.550
        var salon = SalonAggregate.Create(Guid.NewGuid(), "Salon Test", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        var text = DtmfAnnounceService.BuildAnnounceText(salon);

        text.Should().Be("Vous êtes sur le link F5ABC actuellement connecté sur Salon Test.");
        text.Should().NotContain("fréquence");
    }

    [Fact]
    public void BuildAnnounceText_SplitMode_ShouldIncludeTxFrequency()
    {
        // RxFrequency != TxFrequency → annonce de la fréquence TX
        var config = new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic", "svxlink.d", 16000, 1,
            "ref.f5kri.fr", 5300, "F5ABC-L", "test-auth-key-123", 0,
            "F4XYZ", "ModuleHelp", 60, 60,
            null, "fr_FR", 0,
            145.600m, 145.000m, null, null);
        var salon = SalonAggregate.Create(Guid.NewGuid(), "Salon Split", false, false, config)
            .Match(Succ: s => s, Fail: _ => throw new InvalidOperationException());

        var text = DtmfAnnounceService.BuildAnnounceText(salon);

        text.Should().Be("Vous êtes sur le link F4XYZ actuellement connecté sur Salon Split. La fréquence d'émission est 145 virgule 000 mégahertz.");
    }

    // -------------------------------------------------------------------------
    // FormatFrequency — formatage des fréquences pour TTS fr-FR
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(145.550, "145 virgule 550 mégahertz")]
    [InlineData(145.000, "145 virgule 000 mégahertz")]
    [InlineData(438.675, "438 virgule 675 mégahertz")]
    [InlineData(430.100, "430 virgule 100 mégahertz")]
    public void FormatFrequency_ShouldFormatCorrectly(double frequency, string expected)
    {
        var result = DtmfAnnounceService.FormatFrequency((decimal)frequency);
        result.Should().Be(expected);
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
