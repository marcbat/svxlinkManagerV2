using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.SetSalonAsDefault;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour SetSalonAsDefaultCommand et son handler.
/// Règle métier : un seul salon peut être le salon par défaut à la fois.
/// </summary>
public class SetSalonAsDefaultCommandTests
{
    private readonly ISalonRepository _repository;

    public SetSalonAsDefaultCommandTests()
    {
        _repository = Substitute.For<ISalonRepository>();
    }

    [Fact]
    public async Task Handle_WhenNoCurrentDefault_ShouldSetAsDefault()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId, isDefault: false);
        var command = new SetSalonAsDefaultCommand(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        _repository.GetDefaultAsync(Arg.Any<CancellationToken>())
            .Returns((SalonAggregate?)null);
        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await SetSalonAsDefaultCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        salon.IsDefault.Should().BeTrue();

        await _repository.Received(1).SaveAsync(Arg.Is<SalonAggregate>(a => a.Id == salonId && a.IsDefault), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnotherSalonIsDefault_ShouldUnsetOldAndSetNew()
    {
        // Arrange
        var oldDefaultId = Guid.NewGuid();
        var newDefaultId = Guid.NewGuid();

        var oldDefault = CreateValidAggregate(oldDefaultId, isDefault: true);
        var newSalon = CreateValidAggregate(newDefaultId, isDefault: false);
        var command = new SetSalonAsDefaultCommand(newDefaultId);

        _repository.GetByIdAsync(newDefaultId, Arg.Any<CancellationToken>())
            .Returns(newSalon.ToSuccess());
        _repository.GetDefaultAsync(Arg.Any<CancellationToken>())
            .Returns(oldDefault);
        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await SetSalonAsDefaultCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        oldDefault.IsDefault.Should().BeFalse();
        newSalon.IsDefault.Should().BeTrue();

        // l'ancien salon par défaut doit être sauvegardé sans statut default
        await _repository.Received(1).SaveAsync(Arg.Is<SalonAggregate>(a => a.Id == oldDefaultId && !a.IsDefault), Arg.Any<CancellationToken>());
        // le nouveau salon doit être sauvegardé avec statut default
        await _repository.Received(1).SaveAsync(Arg.Is<SalonAggregate>(a => a.Id == newDefaultId && a.IsDefault), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSalonAlreadyDefault_ShouldSucceedWithoutChanges()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId, isDefault: true);
        var command = new SetSalonAsDefaultCommand(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());

        // Act
        var result = await SetSalonAsDefaultCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();

        // Aucune sauvegarde ne doit être effectuée (le salon est déjà par défaut)
        await _repository.DidNotReceive().GetDefaultAsync(Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSalonNotFound_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var command = new SetSalonAsDefaultCommand(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Salon", salonId).ToFailure<SalonAggregate>());

        // Act
        var result = await SetSalonAsDefaultCommandHandler.Handle(command, _repository, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    private static SalonAggregate CreateValidAggregate(Guid id, bool isDefault)
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

        var result = SalonAggregate.Create(id, "Salon Test", isDefault, isTemporized: false, config);
        var aggregate = result.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException("Failed to create aggregate"));

        // Si isDefault, appliquer SetAsDefault (Create crée avec IsDefault=isDefault via l'event SalonCreated)
        // SalonCreated initialise déjà IsDefault correctement depuis le paramètre.
        return aggregate;
    }
}
