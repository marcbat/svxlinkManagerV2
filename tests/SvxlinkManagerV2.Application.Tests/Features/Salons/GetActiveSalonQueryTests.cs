using FluentAssertions;
using LanguageExt;
using NSubstitute;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour GetActiveSalonQuery et son handler
/// </summary>
public class GetActiveSalonQueryTests
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;

    public GetActiveSalonQueryTests()
    {
        _repository = Substitute.For<ISalonRepository>();
        _tracker = Substitute.For<IActiveSessionTracker>();
    }

    [Fact]
    public async Task Handle_WhenActiveSalonExists_ShouldReturnIt()
    {
        // Arrange
        var activeSalonId = Guid.NewGuid();
        var activeSalon = CreateValidAggregate(activeSalonId, "Salon Actif");

        _tracker.ActiveSalonId.Returns((Guid?)activeSalonId);
        _repository.GetByIdAsync(activeSalonId, Arg.Any<CancellationToken>())
            .Returns(activeSalon.ToSuccess());

        var query = new GetActiveSalonQuery();

        // Act
        var result = await new GetActiveSalonQueryHandler(_repository, _tracker).Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Salon Actif");
        result.Id.Should().Be(activeSalonId);

        await _repository.Received(1).GetByIdAsync(activeSalonId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoActiveSalon_ShouldReturnNull()
    {
        // Arrange
        _tracker.ActiveSalonId.Returns((Guid?)null);

        var query = new GetActiveSalonQuery();

        // Act
        var result = await new GetActiveSalonQueryHandler(_repository, _tracker).Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        await _repository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenActiveSalonIsDeleted_ShouldReturnNull()
    {
        // Arrange
        var activeSalonId = Guid.NewGuid();
        var deletedSalon = CreateValidAggregate(activeSalonId, "Salon Supprimé");

        // Soft-delete the salon
        deletedSalon.Delete();
        deletedSalon.ClearDomainEvents();

        _tracker.ActiveSalonId.Returns((Guid?)activeSalonId);
        _repository.GetByIdAsync(activeSalonId, Arg.Any<CancellationToken>())
            .Returns(deletedSalon.ToSuccess());

        var query = new GetActiveSalonQuery();

        // Act
        var result = await new GetActiveSalonQueryHandler(_repository, _tracker).Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenRepositoryFails_ShouldReturnNull()
    {
        // Arrange
        var activeSalonId = Guid.NewGuid();

        _tracker.ActiveSalonId.Returns((Guid?)activeSalonId);
        _repository.GetByIdAsync(activeSalonId, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Salon", activeSalonId).ToFailure<SalonAggregate>());

        var query = new GetActiveSalonQuery();

        // Act
        var result = await new GetActiveSalonQueryHandler(_repository, _tracker).Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    private static SalonAggregate CreateValidAggregate(Guid id, string name)
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
            "test-key",
            0,
            "F5ABC",
            "ModuleHelp",
            60,
            60,
            null,
            "fr_FR",
            0,
            145.550m,
            145.550m,
            136.5m,
            136.5m);

        var result = SalonAggregate.Create(id, name, false, false, config);
        return result.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException("Failed to create aggregate"));
    }
}