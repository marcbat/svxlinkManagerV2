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
            145.550m,  // RxFrequency
            145.550m,  // TxFrequency
            136.5m,    // RxCtcss
            136.5m);   // TxCtcss

        var command = new CreateSalonCommand(
            Guid.NewGuid(),
            "Salon Test",
            false,
            false,
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
            145.550m,        // RxFrequency
            145.550m,        // TxFrequency
            136.5m,          // RxCtcss
            136.5m);         // TxCtcss
    }
}
