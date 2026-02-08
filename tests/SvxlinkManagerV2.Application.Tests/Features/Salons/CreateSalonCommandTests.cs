using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.CreateSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour CreateSalonCommand et son handler
/// </summary>
public class CreateSalonCommandTests
{
    private readonly ISalonRepository _repository;

    public CreateSalonCommandTests()
    {
        _repository = Substitute.For<ISalonRepository>();
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldSucceed()
    {
        // Arrange
        var command = new CreateSalonCommand(
            Guid.NewGuid(),
            "Salon National France",
            IsDefault: true,
            IsTemporized: false,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 136.5m,
            TxCtcss: 136.5m,
            CreateValidConfiguration());

        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await CreateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess(id =>
        {
            id.Should().Be(command.Id);
        });

        await _repository.Received(1).SaveAsync(
            Arg.Is<SalonAggregate>(a => a.Id == command.Id && a.Name == command.Name),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidConfiguration_ShouldFail()
    {
        // Arrange - Configuration avec host vide
        var invalidConfig = new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d",
            16000,
            1,
            "", // Host vide - invalide
            5300,
            "F5ABC-L",
            "test-key",
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
            null,      // SoundId
            0m,        // RxFrequency - sera remplacée par la Command
            0m,        // TxFrequency - sera remplacée par la Command
            null,      // RxCtcss - sera remplacée par la Command
            null);     // TxCtcss - sera remplacée par la Command

        var command = new CreateSalonCommand(
            Guid.NewGuid(),
            "Salon Test",
            false,
            false,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 136.5m,
            TxCtcss: 136.5m,
            invalidConfig);

        // Act
        var result = await CreateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_HOST_REQUIRED");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRepositoryFails_ShouldReturnFailure()
    {
        // Arrange
        var command = new CreateSalonCommand(
            Guid.NewGuid(),
            "Salon Test",
            false,
            false,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 136.5m,
            TxCtcss: 136.5m,
            CreateValidConfiguration());

        var repositoryError = Error.Validation("DB_ERROR", "Erreur de base de données");
        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(repositoryError.ToFailure<Unit>());

        // Act
        var result = await CreateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "DB_ERROR");
        });
    }

    [Fact]
    public async Task Handle_WithInvalidRxFrequency_ShouldFail()
    {
        // Arrange - Fréquence RX invalide (hors plage 30-3000 MHz)
        var command = new CreateSalonCommand(
            Guid.NewGuid(),
            "Salon Test",
            false,
            false,
            RxFrequency: 5000m, // Invalide - hors plage
            TxFrequency: 145.550m,
            RxCtcss: null,
            TxCtcss: null,
            CreateValidConfiguration());

        // Act
        var result = await CreateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

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
        // Arrange - Fréquence TX invalide (hors plage 30-3000 MHz)
        var command = new CreateSalonCommand(
            Guid.NewGuid(),
            "Salon Test",
            false,
            false,
            RxFrequency: 145.550m,
            TxFrequency: 10m, // Invalide - en dessous de 30 MHz
            RxCtcss: null,
            TxCtcss: null,
            CreateValidConfiguration());

        // Act
        var result = await CreateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

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
        // Arrange - CTCSS RX invalide (hors plage 67.0-250.3 Hz)
        var command = new CreateSalonCommand(
            Guid.NewGuid(),
            "Salon Test",
            false,
            false,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 300m, // Invalide - au-dessus de 250.3 Hz
            TxCtcss: null,
            CreateValidConfiguration());

        // Act
        var result = await CreateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_RXCTCSS_INVALID");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidTxCtcss_ShouldFail()
    {
        // Arrange - CTCSS TX invalide (hors plage 67.0-250.3 Hz)
        var command = new CreateSalonCommand(
            Guid.NewGuid(),
            "Salon Test",
            false,
            false,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: null,
            TxCtcss: 50m, // Invalide - en dessous de 67.0 Hz
            CreateValidConfiguration());

        // Act
        var result = await CreateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_TXCTCSS_INVALID");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNullCtcss_ShouldSucceed()
    {
        // Arrange - CTCSS optionnels (null = pas de CTCSS)
        var command = new CreateSalonCommand(
            Guid.NewGuid(),
            "Salon Sans CTCSS",
            false,
            false,
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: null,
            TxCtcss: null,
            CreateValidConfiguration());

        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await CreateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();

        await _repository.Received(1).SaveAsync(
            Arg.Any<SalonAggregate>(),
            Arg.Any<CancellationToken>());
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
            "OPUS",
            0,
            "F5ABC",
            "ModuleHelp,ModuleParrot",
            60,
            60,
            "71.9",
            "/usr/share/svxlink/events.tcl",
            "fr_FR",
            0,
            Guid.NewGuid(),  // SoundId
            0m,              // RxFrequency - sera remplacée par la Command
            0m,              // TxFrequency - sera remplacée par la Command
            null,            // RxCtcss - sera remplacée par la Command
            null);           // TxCtcss - sera remplacée par la Command
    }
}
