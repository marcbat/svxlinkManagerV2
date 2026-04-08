using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Setup;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Setup;

/// <summary>
/// Tests unitaires pour CompleteSetupCommand et son handler.
/// </summary>
public class CompleteSetupCommandTests
{
    private readonly ISalonRepository _salonRepository;
    private readonly IGeneralConfigurationRepository _generalConfigRepository;
    private readonly ISetupStatusService _setupStatusService;
    private readonly ILogger<CompleteSetupCommandHandler> _logger;
    private readonly CompleteSetupCommandHandler _handler;

    public CompleteSetupCommandTests()
    {
        _salonRepository = Substitute.For<ISalonRepository>();
        _generalConfigRepository = Substitute.For<IGeneralConfigurationRepository>();
        _setupStatusService = Substitute.For<ISetupStatusService>();
        _logger = Substitute.For<ILogger<CompleteSetupCommandHandler>>();

        _handler = new CompleteSetupCommandHandler(
            _salonRepository,
            _generalConfigRepository,
            _setupStatusService,
            _logger);
    }

    [Fact]
    public async Task Handle_WhenDataIsValid_ShouldSeed6Salons()
    {
        // Arrange
        var data = new SetupData
        {
            Callsign = "F5ABC",
            SimplexCallsign = "F5ABC-L",
            RxFrequency = 145.500m,
            TxFrequency = 145.500m
        };

        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());
        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _generalConfigRepository.SaveAsync(Arg.Any<GeneralConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await _handler.Handle(new CompleteSetupCommand(data), CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        await _salonRepository.Received(6).SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDataIsValid_SalonsShouldHaveUserCallsign()
    {
        // Arrange
        var data = new SetupData
        {
            Callsign = "F1XYZ",
            SimplexCallsign = "F1XYZ-L",
            RxFrequency = 145.500m,
            TxFrequency = 145.500m
        };

        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());
        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _generalConfigRepository.SaveAsync(Arg.Any<GeneralConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await _handler.Handle(new CompleteSetupCommand(data), CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        await _salonRepository.Received(6).SaveAsync(
            Arg.Is<SalonAggregate>(s =>
                s.Configuration.Callsign == "F1XYZ" &&
                s.Configuration.SimplexCallsign == "F1XYZ-L"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDataIsValid_SalonsShouldHaveUserFrequencies()
    {
        // Arrange
        var data = new SetupData
        {
            Callsign = "F5ABC",
            SimplexCallsign = "F5ABC-L",
            RxFrequency = 430.100m,
            TxFrequency = 430.600m,
            RxCtcss = 88.5m,
            TxCtcss = 88.5m
        };

        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());
        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _generalConfigRepository.SaveAsync(Arg.Any<GeneralConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await _handler.Handle(new CompleteSetupCommand(data), CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        await _salonRepository.Received(6).SaveAsync(
            Arg.Is<SalonAggregate>(s =>
                s.Configuration.RxFrequency == 430.100m &&
                s.Configuration.TxFrequency == 430.600m &&
                s.Configuration.RxCtcss == 88.5m &&
                s.Configuration.TxCtcss == 88.5m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoExistingGeneralConfig_ShouldCreateNewOne()
    {
        // Arrange
        var data = new SetupData
        {
            Callsign = "F5ABC",
            SimplexCallsign = "F5ABC-L",
            RxFrequency = 145.500m,
            TxFrequency = 145.500m
        };

        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());
        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _generalConfigRepository.SaveAsync(Arg.Any<GeneralConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await _handler.Handle(new CompleteSetupCommand(data), CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        await _generalConfigRepository.Received(1).SaveAsync(
            Arg.Is<GeneralConfigurationAggregate>(c =>
                c.DefaultRxFrequency == 145.500m &&
                c.DefaultTxFrequency == 145.500m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExistingGeneralConfig_ShouldUpdateFrequencies()
    {
        // Arrange
        var existing = GeneralConfigurationAggregate.Create(
            startReflectorOnStartup: true,
            startDefaultSalonOnStartup: true,
            defaultRxFrequency: 145.550m,
            defaultTxFrequency: 145.550m).Match(
                Succ: a => { a.ClearDomainEvents(); return a; },
                Fail: _ => throw new InvalidOperationException("Failed to create test GeneralConfigurationAggregate"));

        var data = new SetupData
        {
            Callsign = "F5ABC",
            SimplexCallsign = "F5ABC-L",
            RxFrequency = 430.100m,
            TxFrequency = 430.600m
        };

        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());
        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(existing);
        _generalConfigRepository.SaveAsync(Arg.Any<GeneralConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await _handler.Handle(new CompleteSetupCommand(data), CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        await _generalConfigRepository.Received(1).SaveAsync(
            Arg.Is<GeneralConfigurationAggregate>(c =>
                c.DefaultRxFrequency == 430.100m &&
                c.DefaultTxFrequency == 430.600m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSetupCompleted_ShouldInvalidateStatusCache()
    {
        // Arrange
        var data = new SetupData
        {
            Callsign = "F5ABC",
            SimplexCallsign = "F5ABC-L",
            RxFrequency = 145.550m,
            TxFrequency = 145.550m
        };

        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());
        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _generalConfigRepository.SaveAsync(Arg.Any<GeneralConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await _handler.Handle(new CompleteSetupCommand(data), CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        _setupStatusService.Received(1).InvalidateCache();
    }

    [Fact]
    public async Task Handle_WhenSalonSaveFails_ShouldReturnFailure()
    {
        // Arrange
        var data = new SetupData
        {
            Callsign = "F5ABC",
            SimplexCallsign = "F5ABC-L",
            RxFrequency = 145.550m,
            TxFrequency = 145.550m
        };

        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation("SAVE_ERROR", "Erreur de sauvegarde").ToFailure<Unit>());

        // Act
        var result = await _handler.Handle(new CompleteSetupCommand(data), CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SAVE_ERROR");
        });
        _setupStatusService.DidNotReceive().InvalidateCache();
    }

    [Fact]
    public async Task Handle_WhenGeneralConfigSaveFails_ShouldReturnFailure()
    {
        // Arrange
        var data = new SetupData
        {
            Callsign = "F5ABC",
            SimplexCallsign = "F5ABC-L",
            RxFrequency = 145.550m,
            TxFrequency = 145.550m
        };

        _salonRepository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());
        _generalConfigRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _generalConfigRepository.SaveAsync(Arg.Any<GeneralConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation("CONFIG_ERROR", "Erreur config générale").ToFailure<Unit>());

        // Act
        var result = await _handler.Handle(new CompleteSetupCommand(data), CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "CONFIG_ERROR");
        });
        _setupStatusService.DidNotReceive().InvalidateCache();
    }
}
