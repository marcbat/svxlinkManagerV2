using FluentAssertions;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Reflectors.GetAllReflectors;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;

namespace SvxlinkManagerV2.Application.Tests.Features.Reflectors;

/// <summary>
/// Tests unitaires pour GetAllReflectorsQuery et son handler.
/// </summary>
public class GetAllReflectorsQueryTests
{
    private const string ValidConfig = """
        [GLOBAL]
        TIMESTAMP_FORMAT="%c"
        LISTEN_PORT=5300
        CODECS=OPUS

        [USERS]
        HB9GXP-H=DevNodes

        [PASSWORDS]
        DevNodes="Passw0rd"
        """;

    private readonly IReflectorRepository _repository;

    public GetAllReflectorsQueryTests()
    {
        _repository = Substitute.For<IReflectorRepository>();
    }

    [Fact]
    public async Task Handle_ShouldReturnAllReflectors()
    {
        // Arrange
        var reflector1 = CreateValidAggregate(Guid.NewGuid(), "Reflector 1");
        var reflector2 = CreateValidAggregate(Guid.NewGuid(), "Reflector 2");
        var reflectors = new List<ReflectorAggregate> { reflector1, reflector2 };

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(reflectors.AsReadOnly());

        var query = new GetAllReflectorsQuery();

        // Act
        var result = await new GetAllReflectorsQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Name == "Reflector 1");
        result.Should().Contain(r => r.Name == "Reflector 2");

        await _repository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoReflectors_ShouldReturnEmptyList()
    {
        // Arrange
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ReflectorAggregate>().AsReadOnly());

        var query = new GetAllReflectorsQuery();

        // Act
        var result = await new GetAllReflectorsQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    private static ReflectorAggregate CreateValidAggregate(Guid id, string name)
    {
        var result = ReflectorAggregate.Create(id, name, ValidConfig);
        return result.Match(
            Succ: a =>
            {
                a.ClearDomainEvents();
                return a;
            },
            Fail: _ => throw new InvalidOperationException("La création de l'aggregate test a échoué"));
    }
}
