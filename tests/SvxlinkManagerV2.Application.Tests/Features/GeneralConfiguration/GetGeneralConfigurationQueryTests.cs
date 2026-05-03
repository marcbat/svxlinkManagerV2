using FluentAssertions;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.GeneralConfiguration.Get;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;

namespace SvxlinkManagerV2.Application.Tests.Features.GeneralConfiguration;

/// <summary>
/// Tests unitaires pour GetGeneralConfigurationQuery et son handler.
/// </summary>
public class GetGeneralConfigurationQueryTests
{
    private readonly IGeneralConfigurationRepository _repository;

    public GetGeneralConfigurationQueryTests()
    {
        _repository = Substitute.For<IGeneralConfigurationRepository>();
    }

    [Fact]
    public async Task Handle_WhenConfigurationExists_ShouldReturnConfiguration()
    {
        // Arrange
        var config = CreateValidAggregate();
        _repository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(config);

        var query = new GetGeneralConfigurationQuery();

        // Act
        var result = await new GetGeneralConfigurationQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(GeneralConfigurationAggregate.FixedId);
        result.StartReflectorOnStartup.Should().BeTrue();
        result.StartDefaultSalonOnStartup.Should().BeFalse();

        await _repository.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoConfigurationExists_ShouldReturnNull()
    {
        // Arrange
        _repository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);

        var query = new GetGeneralConfigurationQuery();

        // Act
        var result = await new GetGeneralConfigurationQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();

        await _repository.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    private static GeneralConfigurationAggregate CreateValidAggregate()
    {
        var result = GeneralConfigurationAggregate.Create(
            startReflectorOnStartup: true,
            startDefaultSalonOnStartup: false);

        return result.Match(
            Succ: a => { a.ClearDomainEvents(); return a; },
            Fail: _ => throw new InvalidOperationException("Failed to create test aggregate"));
    }
}
