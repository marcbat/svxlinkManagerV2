using FluentAssertions;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Reflectors.GetReflectorById;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Tests.Features.Reflectors;

/// <summary>
/// Tests unitaires pour GetReflectorByIdQuery et son handler.
/// </summary>
public class GetReflectorByIdQueryTests
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

    public GetReflectorByIdQueryTests()
    {
        _repository = Substitute.For<IReflectorRepository>();
    }

    [Fact]
    public async Task Handle_WhenReflectorExists_ShouldReturnReflector()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var aggregate = CreateValidAggregate(reflectorId);
        var query = new GetReflectorByIdQuery(reflectorId);

        _repository.GetByIdAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());

        // Act
        var result = await new GetReflectorByIdQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess(reflector =>
        {
            reflector.Id.Should().Be(reflectorId);
            reflector.Name.Should().Be("SvxReflector Test");
        });

        await _repository.Received(1).GetByIdAsync(reflectorId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenReflectorNotFound_ShouldFail()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var query = new GetReflectorByIdQuery(reflectorId);
        var notFoundError = Error.NotFound("Reflector", reflectorId);

        _repository.GetByIdAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(notFoundError.ToFailure<ReflectorAggregate>());

        // Act
        var result = await new GetReflectorByIdQueryHandler(_repository).Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });
    }

    private static ReflectorAggregate CreateValidAggregate(Guid id)
    {
        var result = ReflectorAggregate.Create(id, "SvxReflector Test", ValidConfig);
        return result.Match(
            Succ: a =>
            {
                a.ClearDomainEvents();
                return a;
            },
            Fail: _ => throw new InvalidOperationException("La création de l'aggregate test a échoué"));
    }
}
