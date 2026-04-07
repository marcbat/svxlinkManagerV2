using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Infrastructure.SvxLink;
using Xunit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink;

public class ReflectorConnectionAnnouncementServiceTests
{
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly ISvxLinkLogService _logService;
    private readonly ITtsService _ttsService;
    private readonly IDtmfPtyWriter _ptyWriter;
    private readonly ILogger<ReflectorConnectionAnnouncementService> _logger;
    private readonly ReflectorConnectionAnnouncementService _service;

    public ReflectorConnectionAnnouncementServiceTests()
    {
        _connectedNodesService = Substitute.For<IConnectedNodesService>();
        _logService = Substitute.For<ISvxLinkLogService>();
        _ttsService = Substitute.For<ITtsService>();
        _ptyWriter = Substitute.For<IDtmfPtyWriter>();
        _logger = Substitute.For<ILogger<ReflectorConnectionAnnouncementService>>();

        _service = new ReflectorConnectionAnnouncementService(
            _connectedNodesService,
            _logService,
            _ttsService,
            _ptyWriter,
            _logger);
    }

    [Fact]
    public async Task StartAsync_ShouldSubscribeToEvents()
    {
        // Act
        await _service.StartAsync(CancellationToken.None);

        // Assert
        _connectedNodesService.Received(1).OnReset += Arg.Any<Action>();
        _connectedNodesService.Received(1).OnNodesInitialized += Arg.Any<Action<IReadOnlyList<ConnectedNodeInfo>>>();
        _logService.Received(1).OnLogReceived += Arg.Any<Action<SvxLinkLogEntry>>();
    }

    [Fact]
    public async Task StopAsync_ShouldUnsubscribeFromEvents()
    {
        // Arrange
        await _service.StartAsync(CancellationToken.None);

        // Act
        await _service.StopAsync(CancellationToken.None);

        // Assert
        _connectedNodesService.Received(1).OnReset -= Arg.Any<Action>();
        _connectedNodesService.Received(1).OnNodesInitialized -= Arg.Any<Action<IReadOnlyList<ConnectedNodeInfo>>>();
        _logService.Received(1).OnLogReceived -= Arg.Any<Action<SvxLinkLogEntry>>();
    }

    [Fact]
    public async Task OnNodesInitialized_WhenNotArmed_ShouldNotSendDtmf()
    {
        // Arrange — service non armé (OnConnectionReset pas encore appelé)
        await _service.StartAsync(CancellationToken.None);

        // Act
        _service.OnNodesInitialized(new List<ConnectedNodeInfo>().AsReadOnly());
        await Task.Delay(50); // laisser le temps à l'async void de s'exécuter

        // Assert
        await _ptyWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnNodesInitialized_WhenArmed_ShouldSendSuccessAnnouncementCode()
    {
        // Arrange
        await _service.StartAsync(CancellationToken.None);
        _ptyWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LanguageExt.Common.Error, Unit>>(unit));

        _service.OnConnectionReset(); // armer le service

        // Act
        _service.OnNodesInitialized(new List<ConnectedNodeInfo>().AsReadOnly());
        await Task.Delay(100); // laisser le temps à l'async void de s'exécuter

        // Assert — doit envoyer la commande 398 (annonce de connexion réussie)
        await _ptyWriter.Received(1).SendCommandAsync(
            Arg.Is<string>(cmd => cmd == ReflectorConnectionAnnouncementService.SuccessAnnouncementDtmfCode.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnNodesInitialized_WhenArmedTwice_ShouldAnnounceOnlyOnce()
    {
        // Arrange
        await _service.StartAsync(CancellationToken.None);
        _ptyWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LanguageExt.Common.Error, Unit>>(unit));

        _service.OnConnectionReset();

        // Act — déclencher deux fois
        _service.OnNodesInitialized(new List<ConnectedNodeInfo>().AsReadOnly());
        await Task.Delay(50);
        _service.OnNodesInitialized(new List<ConnectedNodeInfo>().AsReadOnly());
        await Task.Delay(50);

        // Assert — l'annonce ne doit être envoyée qu'une seule fois
        await _ptyWriter.Received(1).SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnNodesInitialized_AfterReArming_ShouldAnnounceAgain()
    {
        // Arrange
        await _service.StartAsync(CancellationToken.None);
        _ptyWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LanguageExt.Common.Error, Unit>>(unit));

        // Premier cycle
        _service.OnConnectionReset();
        _service.OnNodesInitialized(new List<ConnectedNodeInfo>().AsReadOnly());
        await Task.Delay(50);

        // Deuxième cycle (nouveau switch de salon)
        _service.OnConnectionReset();
        _service.OnNodesInitialized(new List<ConnectedNodeInfo>().AsReadOnly());
        await Task.Delay(50);

        // Assert — l'annonce doit être envoyée deux fois (une par cycle)
        await _ptyWriter.Received(2).SendCommandAsync(
            Arg.Is<string>(cmd => cmd == ReflectorConnectionAnnouncementService.SuccessAnnouncementDtmfCode.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("ReflectorLogic: Access denied")]
    [InlineData("*** ERROR ReflectorLogic: Access denied: node HB9TEST not allowed")]
    [InlineData("ReflectorLogic: Not authorized to login to the reflector")]
    [InlineData("access denied - check configuration")]
    public void IsConnectionFailureMessage_ShouldDetectAuthErrors(string message)
    {
        // Act & Assert
        ReflectorConnectionAnnouncementService.IsConnectionFailureMessage(message).Should().BeTrue();
    }

    [Theory]
    [InlineData("ReflectorLogic: Connected nodes: HB9GXP-H")]
    [InlineData("ReflectorLogic: Node joined: HB9GXP-H")]
    [InlineData("SvxLink v19.09.2 starting")]
    [InlineData("*** WARNING some warning")]
    public void IsConnectionFailureMessage_ShouldNotFlagNormalMessages(string message)
    {
        // Act & Assert
        ReflectorConnectionAnnouncementService.IsConnectionFailureMessage(message).Should().BeFalse();
    }

    [Fact]
    public async Task OnLogReceived_WhenArmedAndAuthError_ShouldGenerateTtsAndSendPlaybackCode()
    {
        // Arrange
        await _service.StartAsync(CancellationToken.None);
        _ttsService.GenerateWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LanguageExt.Common.Error, string>>(
                Validation<LanguageExt.Common.Error, string>.Success(ReflectorConnectionAnnouncementService.TtsWavPath)));
        _ptyWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LanguageExt.Common.Error, Unit>>(unit));

        _service.OnConnectionReset(); // armer le service

        var failureEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Access denied",
            SvxLinkLogLevel.Error);

        // Act
        _service.OnLogReceived(failureEntry);
        await Task.Delay(100); // laisser le temps à l'async void de s'exécuter

        // Assert — doit générer le TTS et envoyer la commande 399
        await _ttsService.Received(1).GenerateWavAsync(
            Arg.Any<string>(),
            Arg.Is<string>(path => path == ReflectorConnectionAnnouncementService.TtsWavPath),
            Arg.Any<CancellationToken>());
        await _ptyWriter.Received(1).SendCommandAsync(
            Arg.Is<string>(cmd => cmd == ReflectorConnectionAnnouncementService.TtsPlaybackDtmfCode.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnLogReceived_WhenNotArmed_ShouldNotGenerateTts()
    {
        // Arrange — service non armé
        await _service.StartAsync(CancellationToken.None);

        var failureEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Access denied",
            SvxLinkLogLevel.Error);

        // Act
        _service.OnLogReceived(failureEntry);
        await Task.Delay(50);

        // Assert
        await _ttsService.DidNotReceive().GenerateWavAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnLogReceived_WhenArmedAndNormalMessage_ShouldNotGenerateTts()
    {
        // Arrange
        await _service.StartAsync(CancellationToken.None);
        _service.OnConnectionReset();

        var normalEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Connected nodes: HB9GXP-H",
            SvxLinkLogLevel.Info);

        // Act
        _service.OnLogReceived(normalEntry);
        await Task.Delay(50);

        // Assert
        await _ttsService.DidNotReceive().GenerateWavAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnLogReceived_AfterSuccessAnnouncement_ShouldNotAnnounceFailure()
    {
        // Arrange — simuler une connexion réussie suivie d'une erreur tardive
        await _service.StartAsync(CancellationToken.None);
        _ptyWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LanguageExt.Common.Error, Unit>>(unit));

        _service.OnConnectionReset();
        // Succès en premier
        _service.OnNodesInitialized(new List<ConnectedNodeInfo>().AsReadOnly());
        await Task.Delay(50);

        // Ensuite une erreur (tardive, par exemple après reconnexion)
        var failureEntry = new SvxLinkLogEntry(
            DateTime.Now,
            "ReflectorLogic: Access denied",
            SvxLinkLogLevel.Error);
        _service.OnLogReceived(failureEntry);
        await Task.Delay(50);

        // Assert — seule l'annonce de succès doit avoir été envoyée
        await _ttsService.DidNotReceive().GenerateWavAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _ptyWriter.Received(1).SendCommandAsync(
            Arg.Is<string>(cmd => cmd == ReflectorConnectionAnnouncementService.SuccessAnnouncementDtmfCode.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromEvents()
    {
        // Act
        _service.Dispose();

        // Assert
        _connectedNodesService.Received(1).OnReset -= Arg.Any<Action>();
        _connectedNodesService.Received(1).OnNodesInitialized -= Arg.Any<Action<IReadOnlyList<ConnectedNodeInfo>>>();
        _logService.Received(1).OnLogReceived -= Arg.Any<Action<SvxLinkLogEntry>>();
    }
}
