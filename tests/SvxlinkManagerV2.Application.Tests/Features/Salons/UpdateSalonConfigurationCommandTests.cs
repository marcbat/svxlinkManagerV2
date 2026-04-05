using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.UpdateSalonConfiguration;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour UpdateSalonConfigurationCommand et son handler
/// </summary>
public class UpdateSalonConfigurationCommandTests
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;

    public UpdateSalonConfigurationCommandTests()
    {
        _repository = Substitute.For<ISalonRepository>();
        _tracker = Substitute.For<IActiveSessionTracker>();
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldSucceed()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var aggregate = CreateValidSalonAggregate(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());

        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        var command = new UpdateSalonConfigurationCommand(
            salonId,
            RxFrequency: 144.800m, // Nouvelle fréquence RX
            TxFrequency: 144.800m, // Nouvelle fréquence TX
            RxCtcss: 88.5m,        // Nouveau CTCSS RX
            TxCtcss: 88.5m,        // Nouveau CTCSS TX
            CreateValidConfiguration());

        // Act
        var result = await new UpdateSalonConfigurationCommandHandler(_repository, _tracker).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();

        await _repository.Received(1).SaveAsync(
            Arg.Is<SalonAggregate>(a => a.Id == salonId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSalonNotFound_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var notFoundError = Error.NotFound("SALON", salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(notFoundError.ToFailure<SalonAggregate>());

        var command = new UpdateSalonConfigurationCommand(
            salonId,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: null,
            TxCtcss: null,
            CreateValidConfiguration());

        // Act
        var result = await new UpdateSalonConfigurationCommandHandler(_repository, _tracker).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_NOT_FOUND");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidRxFrequency_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var aggregate = CreateValidSalonAggregate(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());

        var command = new UpdateSalonConfigurationCommand(
            salonId,
            RxFrequency: 4000m, // Invalide - hors plage
            TxFrequency: 145.550m,
            RxCtcss: null,
            TxCtcss: null,
            CreateValidConfiguration());

        // Act
        var result = await new UpdateSalonConfigurationCommandHandler(_repository, _tracker).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_RXFREQUENCY_INVALID");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidTxFrequency_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var aggregate = CreateValidSalonAggregate(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());

        var command = new UpdateSalonConfigurationCommand(
            salonId,
            RxFrequency: 145.550m,
            TxFrequency: 15m, // Invalide - en dessous de 30 MHz
            RxCtcss: null,
            TxCtcss: null,
            CreateValidConfiguration());

        // Act
        var result = await new UpdateSalonConfigurationCommandHandler(_repository, _tracker).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_TXFREQUENCY_INVALID");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidRxCtcss_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var aggregate = CreateValidSalonAggregate(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());

        var command = new UpdateSalonConfigurationCommand(
            salonId,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 350m, // Invalide - au-dessus de 250.3 Hz
            TxCtcss: null,
            CreateValidConfiguration());

        // Act
        var result = await new UpdateSalonConfigurationCommandHandler(_repository, _tracker).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_RXCTCSS_INVALID");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSalonIsActive_ShouldFail()
    {
        // Arrange - Créer un salon actif
        var salonId = Guid.NewGuid();
        _ = CreateValidSalonAggregate(salonId);
        _tracker.IsSalonActive(salonId).Returns(true);

        var command = new UpdateSalonConfigurationCommand(
            salonId,
            RxFrequency: 144.800m,
            TxFrequency: 144.800m,
            RxCtcss: null,
            TxCtcss: null,
            CreateValidConfiguration());

        // Act
        var result = await new UpdateSalonConfigurationCommandHandler(_repository, _tracker).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_ACTIVE");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    private static SvxLinkConfiguration CreateValidConfiguration()
    {
        return new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d",
            16000,
            1,
            "ref.f5kri.fr",
            5300,
            "F5ABC-L",
            "test-auth-key-123",
            0,
            "F5ABC",
            "ModuleHelp,ModuleParrot",
            60,
            60,
            "71.9",
            "/usr/share/svxlink/events.tcl",
            "fr_FR",
            0,
            0m,              // RxFrequency - sera remplacée par la Command
            0m,              // TxFrequency - sera remplacée par la Command
            null,            // RxCtcss - sera remplacée par la Command
            null);           // TxCtcss - sera remplacée par la Command
    }

    private static SalonAggregate CreateValidSalonAggregate(Guid id)
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
            "test-auth-key-123",
            0,
            "F5ABC",
            "ModuleHelp,ModuleParrot",
            60,
            60,
            "71.9",
            "/usr/share/svxlink/events.tcl",
            "fr_FR",
            0,
            145.550m,
            145.550m,
            136.5m,
            136.5m);

        var result = SalonAggregate.Create(
            id,
            "Salon Test",
            false,
            false,
            config);

        return result.Match(
            Succ: aggregate => aggregate,
            Fail: _ => throw new InvalidOperationException("Failed to create test aggregate"));
    }
}
