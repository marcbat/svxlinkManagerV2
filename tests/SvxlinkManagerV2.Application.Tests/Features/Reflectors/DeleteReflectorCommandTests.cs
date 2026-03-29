using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Reflectors.DeleteReflector;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Reflectors;

/// <summary>
/// Tests unitaires pour DeleteReflectorCommand et son handler.
/// Le handler bloque la suppression si le reflector est actif.
/// </summary>
public class DeleteReflectorCommandTests
{
    private readonly IReflectorRepository _repository;
    private readonly IActiveSessionTracker _tracker;

    public DeleteReflectorCommandTests()
    {
        _repository = Substitute.For<IReflectorRepository>();
        _tracker = Substitute.For<IActiveSessionTracker>();
    }

    [Fact]
    public async Task Handle_WhenReflectorIsNotActive_ShouldDelete()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var command = new DeleteReflectorCommand(reflectorId);

        _tracker.IsReflectorActive(reflectorId).Returns(false);
        _repository.DeleteAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeSuccess();
        await _repository.Received(1).DeleteAsync(reflectorId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenReflectorIsActive_ShouldFail()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var command = new DeleteReflectorCommand(reflectorId);

        _tracker.IsReflectorActive(reflectorId).Returns(true);

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_ACTIVE");
        });
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRepositoryDeleteFails_ShouldFail()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var command = new DeleteReflectorCommand(reflectorId);
        var deleteError = Error.Validation("DELETE_ERROR", "Erreur lors de la suppression");

        _tracker.IsReflectorActive(reflectorId).Returns(false);
        _repository.DeleteAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(deleteError.ToFailure<Unit>());

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "DELETE_ERROR");
        });
    }

    private Task<Validation<Error, Unit>> CallHandle(DeleteReflectorCommand command)
    {
        var handler = new DeleteReflectorCommandHandler(_repository, _tracker);
        return handler.Handle(command, CancellationToken.None);
    }
}
