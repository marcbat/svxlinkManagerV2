using FluentAssertions;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.GetAllSalons;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour GetAllSalonsQuery et son handler
/// </summary>
public class GetAllSalonsQueryTests
{
    private readonly ISalonRepository _repository;

    public GetAllSalonsQueryTests()
    {
        _repository = Substitute.For<ISalonRepository>();
    }

    [Fact]
    public async Task Handle_ShouldReturnAllSalons()
    {
        // Arrange
        var salon1 = CreateValidAggregate(Guid.NewGuid(), "Salon 1");
        var salon2 = CreateValidAggregate(Guid.NewGuid(), "Salon 2");
        var salons = new List<SalonAggregate> { salon1, salon2 };

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(salons.AsReadOnly());

        var query = new GetAllSalonsQuery();

        // Act
        var result = await new GetAllSalonsQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Name == "Salon 1");
        result.Should().Contain(s => s.Name == "Salon 2");

        await _repository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoSalons_ShouldReturnEmptyList()
    {
        // Arrange
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SalonAggregate>().AsReadOnly());

        var query = new GetAllSalonsQuery();

        // Act
        var result = await new GetAllSalonsQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
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
            ReflectorProtocol.V2,
            null,
            "F5ABC",
            "ModuleHelp",
            60,
            60,
            null,
            "fr_FR",
            0,
            145.550m, // RxFrequency
            145.550m, // TxFrequency
            136.5m,   // RxCtcss
            136.5m);  // TxCtcss

        var result = SalonAggregate.Create(id, name, false, config);
        return result.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException("Failed to create aggregate"));
    }
}
