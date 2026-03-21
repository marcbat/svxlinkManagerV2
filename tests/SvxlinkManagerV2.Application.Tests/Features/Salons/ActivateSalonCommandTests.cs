using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour ActivateSalonCommand et son handler.
/// Le handler orchestre : configuration SA818, generation svxlink.conf, 
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
    private readonly ILogger _logger;

    public ActivateSalonCommandTests()
    {
        _repository = Substitute.For<ISalonRepository>();
        _tracker = Substitute.For<IActiveSessionTracker>();
        _sa818Repository = Substitute.For<ISA818Repository>();
        _sa818Service = Substitute.For<ISA818Service>();
        _configurationService = Substitute.For<ISvxLinkConfigurationService>();
        _daemonService = Substitute.For<ISvxLinkDaemonService>();
        _connectedNodesService = Substitute.For<IConnectedNodesService>();
        _logger = Substitute.For<ILogger>();
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

    private Task<Validation<Error, Unit>> CallHandle(ActivateSalonCommand command) =>
        ActivateSalonCommandHandler.Handle(
            command,
            _repository,
            _tracker,
            _sa818Repository,
            _sa818Service,
            _configurationService,
            _daemonService,
            _connectedNodesService,
            _logger,
            CancellationToken.None);

    private static SalonAggregate CreateValidAggregate(Guid id)
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
            null,
            145.550m,
            145.550m,
            136.5m,
            136.5m);
        var result = SalonAggregate.Create(id, "Salon Test", false, false, config);
        return result.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException("Failed to create aggregate"));
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