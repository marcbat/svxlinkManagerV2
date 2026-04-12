using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.DeleteSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour DeleteSalonCommand et son handler.
/// </summary>
public class DeleteSalonCommandTests
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;

    public DeleteSalonCommandTests()
    {
        _repository = Substitute.For<ISalonRepository>();
        _tracker = Substitute.For<IActiveSessionTracker>();
    }

    [Fact]
    public async Task Handle_WhenSalonNotActiveAndExists_ShouldDeleteSuccessfully()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId, isDefault: false);
        var command = new DeleteSalonCommand(salonId);

        _tracker.IsSalonActive(salonId).Returns(false);
        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());
        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await new DeleteSalonCommandHandler(_repository, _tracker).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        salon.IsDeleted.Should().BeTrue();

        await _repository.Received(1).SaveAsync(
            Arg.Is<SalonAggregate>(a => a.Id == salonId && a.IsDeleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSalonIsActive_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var command = new DeleteSalonCommand(salonId);

        _tracker.IsSalonActive(salonId).Returns(true);

        // Act
        var result = await new DeleteSalonCommandHandler(_repository, _tracker).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_ACTIVE");
        });

        await _repository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSalonNotFound_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var command = new DeleteSalonCommand(salonId);

        _tracker.IsSalonActive(salonId).Returns(false);
        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Salon", salonId).ToFailure<SalonAggregate>());

        // Act
        var result = await new DeleteSalonCommandHandler(_repository, _tracker).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSalonIsDefault_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var salon = CreateValidAggregate(salonId, isDefault: true);
        var command = new DeleteSalonCommand(salonId);

        _tracker.IsSalonActive(salonId).Returns(false);
        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(salon.ToSuccess());

        // Act
        var result = await new DeleteSalonCommandHandler(_repository, _tracker).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_IS_DEFAULT");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    private static SalonAggregate CreateValidAggregate(Guid id, bool isDefault)
    {
        var config = new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d",
            16000, 1,
            "ref.f5kri.fr", 5300,
            "F5ABC-L", "test-auth-key",
            0,
            ReflectorProtocol.V2, null,
            "F5ABC", "ModuleHelp",
            60, 60,
            null,
            "fr_FR", 0,
            145.550m, 145.550m, 136.5m, 136.5m);

        var result = SalonAggregate.Create(id, "Salon Test", isDefault, false, config);
        return result.Match(
            Succ: a => { a.ClearDomainEvents(); return a; },
            Fail: _ => throw new InvalidOperationException("Failed to create test aggregate"));
    }
}
