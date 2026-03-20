using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour ActivateSalonCommand et son handler
/// </summary>
public class ActivateSalonCommandTests
{
    private readonly ISalonRepository _repository;

    public ActivateSalonCommandTests()
    {
        _repository = Substitute.For<ISalonRepository>();
    }

    [Fact]
    public async Task Handle_WithValidSalon_ShouldActivate()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId);
        var command = new ActivateSalonCommand(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        _repository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns((SalonAggregate?)null);
        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await ActivateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            salon.IsActive.Should().BeTrue();
        });

        await _repository.Received(1).GetByIdAsync(salonId, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveAsync(Arg.Is<SalonAggregate>(a => a.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnotherSalonIsActive_ShouldDeactivateItFirst()
    {
        // Arrange
        var currentActiveId = Guid.NewGuid();
        var newSalonId = Guid.NewGuid();

        var currentActive = CreateValidAggregate(currentActiveId);
        currentActive.Activate();

        var newSalon = CreateValidAggregate(newSalonId);
        var command = new ActivateSalonCommand(newSalonId);

        _repository.GetByIdAsync(newSalonId, Arg.Any<CancellationToken>())
            .Returns(newSalon.ToSuccess());
        _repository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(currentActive);
        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await ActivateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        currentActive.IsActive.Should().BeFalse();
        newSalon.IsActive.Should().BeTrue();

        // l'ancien salon actif doit être sauvegardé désactivé
        await _repository.Received(1).SaveAsync(Arg.Is<SalonAggregate>(a => a.Id == currentActiveId && !a.IsActive), Arg.Any<CancellationToken>());
        // le nouveau salon doit être sauvegardé activé
        await _repository.Received(1).SaveAsync(Arg.Is<SalonAggregate>(a => a.Id == newSalonId && a.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSameActiveSalonActivatedAgain_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId);
        salon.Activate();
        var command = new ActivateSalonCommand(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        // Le salon courant actif est le même que celui qu'on essaie d'activer
        _repository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(salon);

        // Act
        var result = await ActivateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_ALREADY_ACTIVE");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
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
        var result = await ActivateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSalonAlreadyActive_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId);
        salon.Activate(); // Déjà actif
        var command = new ActivateSalonCommand(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        _repository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns((SalonAggregate?)null);

        // Act
        var result = await ActivateSalonCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_ALREADY_ACTIVE");
        });
    }

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
            null,      // SoundId
            145.550m,  // RxFrequency
            145.550m,  // TxFrequency
            136.5m,    // RxCtcss
            136.5m);   // TxCtcss

        var result = SalonAggregate.Create(id, "Salon Test", false, false, config);
        return result.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException("Failed to create aggregate"));
    }
}
