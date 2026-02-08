using FluentAssertions;
using LanguageExt.UnitTesting;
using NSubstitute;
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

    public GetActiveSalonQueryTests()
    {
        _repository = Substitute.For<ISalonRepository>();
    }

    [Fact]
    public async Task Handle_WhenActiveSalonExists_ShouldReturnIt()
    {
        // Arrange
        var activeSalon = CreateValidAggregate(Guid.NewGuid(), "Salon Actif");
        activeSalon.Activate();

        _repository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(activeSalon);

        var query = new GetActiveSalonQuery();

        // Act
        var result = await GetActiveSalonQueryHandler.Handle(query, _repository, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Salon Actif");
        result.IsActive.Should().BeTrue();

        await _repository.Received(1).GetActiveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoActiveSalon_ShouldReturnNull()
    {
        // Arrange
        _repository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns((SalonAggregate?)null);

        var query = new GetActiveSalonQuery();

        // Act
        var result = await GetActiveSalonQueryHandler.Handle(query, _repository, CancellationToken.None);

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

        var result = SalonAggregate.Create(id, name, false, false, config);
        return result.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException("Failed to create aggregate"));
    }
}
