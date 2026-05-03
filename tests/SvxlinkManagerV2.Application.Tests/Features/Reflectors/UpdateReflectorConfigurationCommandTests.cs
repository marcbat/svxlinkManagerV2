using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Reflectors.UpdateReflectorConfiguration;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Reflectors;

/// <summary>
/// Tests unitaires pour UpdateReflectorConfigurationCommand et son handler.
/// Le handler bloque la mise à jour si le reflector est actif.
/// </summary>
public class UpdateReflectorConfigurationCommandTests
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
    private readonly IActiveSessionTracker _tracker;

    public UpdateReflectorConfigurationCommandTests()
    {
        _repository = Substitute.For<IReflectorRepository>();
        _tracker = Substitute.For<IActiveSessionTracker>();
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldUpdateConfiguration()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var aggregate = CreateValidAggregate(reflectorId);
        var updatedConfig = ValidConfig.Replace("5300", "5400");
        var command = new UpdateReflectorConfigurationCommand(reflectorId, "Nouveau Nom", updatedConfig);

        _tracker.IsReflectorActive(reflectorId).Returns(false);
        _repository.GetByIdAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());
        _repository.SaveAsync(Arg.Any<ReflectorAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeSuccess();
        await _repository.Received(1).SaveAsync(
            Arg.Is<ReflectorAggregate>(a => a.Name == "Nouveau Nom" && a.Config == updatedConfig),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenReflectorIsActive_ShouldFail()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var command = new UpdateReflectorConfigurationCommand(reflectorId, "Nouveau Nom", ValidConfig);

        _tracker.IsReflectorActive(reflectorId).Returns(true);

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_ACTIVE");
        });
        await _repository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveAsync(Arg.Any<ReflectorAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenReflectorNotFound_ShouldFail()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var command = new UpdateReflectorConfigurationCommand(reflectorId, "Nouveau Nom", ValidConfig);
        var notFoundError = Error.NotFound("Reflector", reflectorId);

        _tracker.IsReflectorActive(reflectorId).Returns(false);
        _repository.GetByIdAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(notFoundError.ToFailure<ReflectorAggregate>());

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });
        await _repository.DidNotReceive().SaveAsync(Arg.Any<ReflectorAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAggregateUpdateFails_ShouldFail()
    {
        // Arrange - nom vide déclenche l'erreur de validation de l'aggregate
        var reflectorId = Guid.NewGuid();
        var aggregate = CreateValidAggregate(reflectorId);
        var command = new UpdateReflectorConfigurationCommand(reflectorId, "", ValidConfig);

        _tracker.IsReflectorActive(reflectorId).Returns(false);
        _repository.GetByIdAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_NAME_REQUIRED");
        });
        await _repository.DidNotReceive().SaveAsync(Arg.Any<ReflectorAggregate>(), Arg.Any<CancellationToken>());
    }

    private Task<Validation<Error, Unit>> CallHandle(UpdateReflectorConfigurationCommand command)
    {
        var handler = new UpdateReflectorConfigurationCommandHandler(_repository, _tracker);
        return handler.Handle(command, CancellationToken.None);
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
