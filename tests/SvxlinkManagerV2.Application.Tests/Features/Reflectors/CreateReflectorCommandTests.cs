using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Reflectors.CreateReflector;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Reflectors;

/// <summary>
/// Tests unitaires pour CreateReflectorCommand et son handler.
/// Le handler crée l'aggregate via la factory method du domaine puis persiste.
/// </summary>
public class CreateReflectorCommandTests
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

    public CreateReflectorCommandTests()
    {
        _repository = Substitute.For<IReflectorRepository>();
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateAndReturnId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var command = new CreateReflectorCommand(id, "SvxReflector Local", ValidConfig);

        _repository.SaveAsync(Arg.Any<ReflectorAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await new CreateReflectorCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess(returnedId =>
        {
            returnedId.Should().Be(id);
        });

        await _repository.Received(1).SaveAsync(
            Arg.Is<ReflectorAggregate>(a => a.Id == id && a.Name == "SvxReflector Local"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEmptyName_ShouldFail()
    {
        // Arrange
        var command = new CreateReflectorCommand(Guid.NewGuid(), "", ValidConfig);

        // Act
        var result = await new CreateReflectorCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_NAME_REQUIRED");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<ReflectorAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidConfig_ShouldFail()
    {
        // Arrange - config sans section [GLOBAL]
        var invalidConfig = """
            [USERS]
            HB9GXP-H=DevNodes
            """;
        var command = new CreateReflectorCommand(Guid.NewGuid(), "SvxReflector Local", invalidConfig);

        // Act
        var result = await new CreateReflectorCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_CONFIG_INVALID");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<ReflectorAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRepositorySaveFails_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var command = new CreateReflectorCommand(id, "SvxReflector Local", ValidConfig);
        var saveError = Error.Validation("SAVE_ERROR", "Erreur lors de la sauvegarde");

        _repository.SaveAsync(Arg.Any<ReflectorAggregate>(), Arg.Any<CancellationToken>())
            .Returns(saveError.ToFailure<Unit>());

        // Act
        var result = await new CreateReflectorCommandHandler(_repository).Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SAVE_ERROR");
        });
    }
}
