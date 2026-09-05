using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Features.Salons.ActivateStandaloneMode;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;
using LangExtError = LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour ActivateStandaloneModeCommand et son handler.
/// Le handler orchestre : récupération config, configuration SA818 (optionnel),
/// génération svxlink.conf standalone et démarrage du daemon.
/// </summary>
public class ActivateStandaloneModeCommandTests
{
    private readonly IGeneralConfigurationRepository _generalConfigRepository;
    private readonly ISA818Repository _sa818Repository;
    private readonly ISA818Service _sa818Service;
    private readonly ISvxLinkConfigurationService _configurationService;
    private readonly ISvxLinkDaemonService _daemonService;
    private readonly IActiveSessionTracker _tracker;
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly IReflectorLinkStateService _linkStateService;
    private readonly IActivityRecorder _activityRecorder;
    private readonly ILogger<ActivateStandaloneModeCommandHandler> _logger;

    public ActivateStandaloneModeCommandTests()
    {
        _generalConfigRepository = Substitute.For<IGeneralConfigurationRepository>();
        _sa818Repository = Substitute.For<ISA818Repository>();
        _sa818Service = Substitute.For<ISA818Service>();
        _configurationService = Substitute.For<ISvxLinkConfigurationService>();
        _daemonService = Substitute.For<ISvxLinkDaemonService>();
        _tracker = Substitute.For<IActiveSessionTracker>();
        _connectedNodesService = Substitute.For<IConnectedNodesService>();
        _linkStateService = Substitute.For<IReflectorLinkStateService>();
        _activityRecorder = Substitute.For<IActivityRecorder>();
        _logger = Substitute.For<ILogger<ActivateStandaloneModeCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WithGeneralConfig_ShouldUseConfiguredFrequencies()
    {
        // Arrange
        var command = new ActivateStandaloneModeCommand();
        var generalConfig = CreateValidGeneralConfig(rxFreq: 144.800m, txFreq: 144.200m);

        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(generalConfig);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(CreateValidSA818Config());
        _sa818Service.ConfigureAsync(Arg.Any<SA818CommandSet>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _configurationService.GenerateStandaloneAsync(Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<ReflectorProtocol>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _tracker.ActiveSalonId.Returns((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        await _configurationService.Received(1).GenerateStandaloneAsync(
            144.800m, 144.200m,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutGeneralConfig_ShouldUseDefaultFrequencies()
    {
        // Arrange
        var command = new ActivateStandaloneModeCommand();

        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns((SA818ConfigurationDto?)null);
        _configurationService.GenerateStandaloneAsync(Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<ReflectorProtocol>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _tracker.ActiveSalonId.Returns((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        // Should use default frequencies (145.550)
        await _configurationService.Received(1).GenerateStandaloneAsync(
            145.550m, 145.550m,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenActiveSalonExists_ShouldStopItFirst()
    {
        // Arrange
        var command = new ActivateStandaloneModeCommand();
        var activeSalonId = Guid.NewGuid();

        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _tracker.ActiveSalonId.Returns((Guid?)activeSalonId);
        _daemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns((SA818ConfigurationDto?)null);
        _configurationService.GenerateStandaloneAsync(Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<ReflectorProtocol>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        await _daemonService.Received(1).StopAsync(Arg.Any<CancellationToken>());
        _tracker.Received(1).SetActiveSalon(null);
        _connectedNodesService.Received(1).Reset();
    }

    [Fact]
    public async Task Handle_WhenActiveSalonExistsAndStopFails_ShouldReturnFailure()
    {
        // Arrange
        var command = new ActivateStandaloneModeCommand();
        var activeSalonId = Guid.NewGuid();

        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _tracker.ActiveSalonId.Returns((Guid?)activeSalonId);
        _daemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<LangExtError, Unit>.Fail(Seq1<LangExtError>(LangExtError.New("Erreur arrêt")))));

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SVXLINK_STOP_ERROR");
        });
    }

    [Fact]
    public async Task Handle_WhenConfigGenerationFails_ShouldReturnFailure()
    {
        // Arrange
        var command = new ActivateStandaloneModeCommand();

        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns((SA818ConfigurationDto?)null);
        _configurationService.GenerateStandaloneAsync(Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<LangExtError, Unit>.Fail(Seq1<LangExtError>(LangExtError.New("Erreur config")))));
        _tracker.ActiveSalonId.Returns((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SVXLINK_CONFIG_ERROR");
        });
    }

    [Fact]
    public async Task Handle_WhenDaemonRestartFails_ShouldReturnFailure()
    {
        // Arrange
        var command = new ActivateStandaloneModeCommand();

        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns((SA818ConfigurationDto?)null);
        _configurationService.GenerateStandaloneAsync(Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<ReflectorProtocol>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<LangExtError, Unit>.Fail(Seq1<LangExtError>(LangExtError.New("Erreur restart")))));
        _tracker.ActiveSalonId.Returns((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SVXLINK_RESTART_ERROR");
        });
    }

    [Fact]
    public async Task Handle_WhenSA818ConfigNotFound_ShouldContinueWithoutSA818()
    {
        // Arrange
        var command = new ActivateStandaloneModeCommand();

        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns((SA818ConfigurationDto?)null);
        _configurationService.GenerateStandaloneAsync(Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<ReflectorProtocol>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _tracker.ActiveSalonId.Returns((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        await _sa818Service.DidNotReceive().ConfigureAsync(Arg.Any<SA818CommandSet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldMarkLinkNotApplicable()
    {
        // Arrange : en mode autonome, svxlink.conf ne contient pas de ReflectorLogic
        var command = new ActivateStandaloneModeCommand();
        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(CreateValidGeneralConfig());
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns((SA818ConfigurationDto?)null);
        _configurationService.GenerateStandaloneAsync(
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<ReflectorProtocol>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));
        _tracker.ActiveSalonId.Returns((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        _linkStateService.Received(1).MarkNotApplicable();
        _linkStateService.DidNotReceive().BeginConnecting();
    }

    private ActivateStandaloneModeCommandHandler CreateHandler()
        => new(
            _generalConfigRepository,
            _sa818Repository,
            _sa818Service,
            _configurationService,
            _daemonService,
            _tracker,
            _connectedNodesService,
            _linkStateService,
            _activityRecorder,
            _logger);

    private static GeneralConfigurationAggregate CreateValidGeneralConfig(
        decimal rxFreq = 145.550m,
        decimal txFreq = 145.550m)
    {
        var result = GeneralConfigurationAggregate.Create(
            startReflectorOnStartup: false,
            startDefaultSalonOnStartup: false,
            defaultRxFrequency: rxFreq,
            defaultTxFrequency: txFreq);

        return result.Match(
            Succ: a => { a.ClearDomainEvents(); return a; },
            Fail: _ => throw new InvalidOperationException("Failed to create test aggregate"));
    }

    private static SA818ConfigurationDto CreateValidSA818Config() =>
        new()
        {
            Id = Guid.NewGuid(),
            Volume = 5,
            Squelch = 3,
            Bandwidth = SA818Bandwidth.Wide25kHz,
            PreEmph = false,
            HighPass = false,
            LowPass = false,
            UpdatedAt = DateTime.UtcNow,
            RxFrequency = 145.550m,
            TxFrequency = 145.550m,
            RxCtcss = null,
            TxCtcss = null
        };
}
