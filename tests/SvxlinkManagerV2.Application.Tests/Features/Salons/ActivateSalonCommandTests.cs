using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Tests.Features.Salons.Sound;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour ActivateSalonCommand et son handler.
/// Le handler orchestre : configuration SA818, deploiement du son, generation svxlink.conf, 
/// restart daemon et mise a jour du tracker d'etat runtime.
/// </summary>
public class ActivateSalonCommandTests
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;
    private readonly ISA818Repository _sa818Repository;
    private readonly ISA818Service _sa818Service;
    private readonly ISvxLinkConfigurationService _configurationService;
    private readonly ISvxLinkDaemonService _daemonService;
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly ISoundRepository _soundRepository;
    private readonly ISoundFileDeploymentService _soundDeploymentService;
    private readonly ILogger<ActivateSalonCommandHandler> _logger;

    public ActivateSalonCommandTests()
    {
        _repository = Substitute.For<ISalonRepository>();
        _tracker = Substitute.For<IActiveSessionTracker>();
        _sa818Repository = Substitute.For<ISA818Repository>();
        _sa818Service = Substitute.For<ISA818Service>();
        _configurationService = Substitute.For<ISvxLinkConfigurationService>();
        _daemonService = Substitute.For<ISvxLinkDaemonService>();
        _connectedNodesService = Substitute.For<IConnectedNodesService>();
        _soundRepository = Substitute.For<ISoundRepository>();
        _soundDeploymentService = Substitute.For<ISoundFileDeploymentService>();
        _logger = Substitute.For<ILogger<ActivateSalonCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WithValidSalon_ShouldActivateAndUpdateTracker()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId);
        var command = new ActivateSalonCommand(salonId);
        var sa818Config = CreateValidSA818Config();

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(sa818Config);
        _sa818Service.ConfigureAsync(Arg.Any<SA818CommandSet>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _soundDeploymentService.CleanupAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _configurationService.GenerateAsync(Arg.Any<SalonAggregate>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeSuccess();
        _tracker.Received(1).SetActiveSalon(salonId);
    }

    [Fact]
    public async Task Handle_WithValidSalon_ShouldPassExactValuesToConfigService()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId);
        var command = new ActivateSalonCommand(salonId);
        var sa818Config = CreateValidSA818Config();

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(sa818Config);
        _sa818Service.ConfigureAsync(Arg.Any<SA818CommandSet>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _soundDeploymentService.CleanupAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _configurationService.GenerateAsync(Arg.Any<SalonAggregate>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeSuccess();
        await _configurationService.Received(1).GenerateAsync(
            Arg.Is<SalonAggregate>(s =>
                s.Id == salonId &&
                s.Configuration.Host == "ref.f5kri.fr" &&
                s.Configuration.Port == 5300 &&
                s.Configuration.Callsign == "F5ABC-L" &&
                s.Configuration.AuthKey == "test-auth-key"),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSoundId_ShouldDeploySound()
    {
        // Arrange — l'annonce one-shot est gérée par Logic.tcl, pas par un paramètre de GenerateAsync
        var salonId = Guid.NewGuid();
        var soundId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId, soundId: soundId);
        var sound = SalonSoundTestHelpers.CreateValidSoundAggregate(soundId, "annonce-test");
        var command = new ActivateSalonCommand(salonId);
        var sa818Config = CreateValidSA818Config();
        const string deployedPath = "/usr/share/svxlink/sounds/fr_FR/svxlinkmanager/Name.wav";

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(sa818Config);
        _sa818Service.ConfigureAsync(Arg.Any<SA818CommandSet>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _soundRepository.GetByIdAsync(soundId, Arg.Any<CancellationToken>())
            .Returns(sound.ToSuccess());
        _soundDeploymentService.DeployAsync(sound, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, string>>(deployedPath));
        _configurationService.GenerateAsync(Arg.Any<SalonAggregate>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle(command);

        // Assert — DeployAsync appelé, GenerateAsync sans paramètre d'annonce
        result.ShouldBeSuccess();
        await _soundDeploymentService.Received(1).DeployAsync(sound, Arg.Any<CancellationToken>());
        await _configurationService.Received(1).GenerateAsync(
            Arg.Any<SalonAggregate>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutSoundId_ShouldCallCleanupAndGenerateConfig()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId, soundId: null);
        var command = new ActivateSalonCommand(salonId);
        var sa818Config = CreateValidSA818Config();

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(sa818Config);
        _sa818Service.ConfigureAsync(Arg.Any<SA818CommandSet>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _soundDeploymentService.CleanupAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _configurationService.GenerateAsync(Arg.Any<SalonAggregate>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle(command);

        // Assert — CleanupAsync appelé pour supprimer Name.wav résiduel
        result.ShouldBeSuccess();
        await _soundDeploymentService.Received(1).CleanupAsync(Arg.Any<CancellationToken>());
        await _soundDeploymentService.DidNotReceive().DeployAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>());
        await _configurationService.Received(1).GenerateAsync(
            Arg.Any<SalonAggregate>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSoundIdButSoundNotFound_ShouldContinueWithoutAnnounce()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var soundId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId, soundId: soundId);
        var command = new ActivateSalonCommand(salonId);
        var sa818Config = CreateValidSA818Config();

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(sa818Config);
        _sa818Service.ConfigureAsync(Arg.Any<SA818CommandSet>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _soundRepository.GetByIdAsync(soundId, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Sound", soundId).ToFailure<SoundAggregate>());
        _configurationService.GenerateAsync(Arg.Any<SalonAggregate>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle(command);

        // Assert — l'activation continue malgré le son introuvable (résilience)
        result.ShouldBeSuccess();
        await _soundDeploymentService.DidNotReceive().DeployAsync(Arg.Any<SoundAggregate>(), Arg.Any<CancellationToken>());
        await _configurationService.Received(1).GenerateAsync(
            Arg.Any<SalonAggregate>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        _tracker.Received(1).SetActiveSalon(salonId);
    }

    [Fact]
    public async Task Handle_WhenSalonNotFound_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var command = new ActivateSalonCommand(salonId);
        var notFoundError = Error.NotFound("Salon", salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(notFoundError.ToFailure<SalonAggregate>());

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });
        _tracker.DidNotReceive().SetActiveSalon(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Handle_WhenAnotherSalonIsActive_ShouldStopDaemonFirst()
    {
        // Arrange
        var activeSalonId = Guid.NewGuid();
        var newSalonId = Guid.NewGuid();
        var newSalon = CreateValidAggregate(newSalonId);
        var command = new ActivateSalonCommand(newSalonId);
        var sa818Config = CreateValidSA818Config();

        _tracker.ActiveSalonId.Returns((Guid?)activeSalonId);
        _repository.GetByIdAsync(newSalonId, Arg.Any<CancellationToken>())
            .Returns(newSalon.ToSuccess());
        _daemonService.StopAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(sa818Config);
        _sa818Service.ConfigureAsync(Arg.Any<SA818CommandSet>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _soundDeploymentService.CleanupAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _configurationService.GenerateAsync(Arg.Any<SalonAggregate>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeSuccess();
        await _daemonService.Received(1).StopAsync(Arg.Any<CancellationToken>());
        _connectedNodesService.Received(1).Reset();
        _tracker.Received(1).SetActiveSalon(null);
        _tracker.Received(1).SetActiveSalon(newSalonId);
    }

    [Fact]
    public async Task Handle_WhenSA818ConfigNotFound_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId);
        var command = new ActivateSalonCommand(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns((SA818ConfigurationDto?)null);

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SA818_CONFIG_NOT_FOUND");
        });
        _tracker.DidNotReceive().SetActiveSalon(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Handle_WhenDaemonRestartFails_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId);
        var command = new ActivateSalonCommand(salonId);
        var sa818Config = CreateValidSA818Config();

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        _tracker.ActiveSalonId.Returns((Guid?)null);
        _sa818Repository.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(sa818Config);
        _sa818Service.ConfigureAsync(Arg.Any<SA818CommandSet>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _soundDeploymentService.CleanupAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _configurationService.GenerateAsync(Arg.Any<SalonAggregate>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(global::LanguageExt.Common.Error.New("SVXLINK_RESTART_ERROR")));

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SVXLINK_RESTART_ERROR");
        });
        _tracker.DidNotReceive().SetActiveSalon(Arg.Any<Guid>());
    }

    private Task<Validation<Error, Unit>> CallHandle(ActivateSalonCommand command)
    {
        var handler = new ActivateSalonCommandHandler(
            _repository, _tracker, _sa818Repository, _sa818Service,
            _configurationService, _daemonService, _connectedNodesService,
            _soundRepository, _soundDeploymentService, _logger);
        return handler.Handle(command, CancellationToken.None);
    }

    private static SalonAggregate CreateValidAggregate(Guid id, Guid? soundId = null)
    {
        var config = new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d",
            16000,
            1,
            "ref.f5kri.fr",
            5300,
            "F5ABC-L",
            "test-auth-key",
            "OPUS",
            0,
            "F5ABC",
            "ModuleHelp",
            60,
            60,
            null,
            "/usr/share/svxlink/events.tcl",
            "fr_FR",
            0,
            145.550m,
            145.550m,
            136.5m,
            136.5m);
        var result = SalonAggregate.Create(id, "Salon Test", false, false, config);
        var aggregate = result.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException("Failed to create aggregate"));

        if (soundId.HasValue)
            aggregate.AssignSound(soundId.Value);

        return aggregate;
    }

    private static SA818ConfigurationDto CreateValidSA818Config() => new()
    {
        Id = Guid.NewGuid(),
        Volume = 4,
        Squelch = 2,
        Bandwidth = SA818Bandwidth.Wide25kHz,
        PreEmph = false,
        HighPass = false,
        LowPass = false,
        UpdatedAt = DateTime.UtcNow
    };
}