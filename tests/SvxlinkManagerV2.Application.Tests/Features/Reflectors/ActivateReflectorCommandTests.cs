using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Reflectors.ActivateReflector;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.Reflectors;

/// <summary>
/// Tests unitaires pour ActivateReflectorCommand et son handler.
/// Le handler orchestre : lecture du reflector, écriture du fichier de config,
/// redémarrage du daemon et mise à jour du tracker d'état runtime.
/// </summary>
public class ActivateReflectorCommandTests
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
    private readonly IReflectorConfigurationService _configurationService;
    private readonly IReflectorDaemonService _daemonService;
    private readonly ILogger<ActivateReflectorCommandHandler> _logger;

    public ActivateReflectorCommandTests()
    {
        _repository = Substitute.For<IReflectorRepository>();
        _tracker = Substitute.For<IActiveSessionTracker>();
        _configurationService = Substitute.For<IReflectorConfigurationService>();
        _daemonService = Substitute.For<IReflectorDaemonService>();
        _logger = Substitute.For<ILogger<ActivateReflectorCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WithValidReflector_ShouldActivateAndUpdateTracker()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var aggregate = CreateValidAggregate(reflectorId);
        var command = new ActivateReflectorCommand(reflectorId);

        _repository.GetByIdAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());
        _configurationService.WriteConfigAsync(aggregate, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeSuccess();
        _tracker.Received(1).SetActiveReflector(reflectorId);
    }

    [Fact]
    public async Task Handle_WhenReflectorNotFound_ShouldFail()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var command = new ActivateReflectorCommand(reflectorId);
        var notFoundError = Error.NotFound("Reflector", reflectorId);

        _repository.GetByIdAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(notFoundError.ToFailure<ReflectorAggregate>());

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });
        _tracker.DidNotReceive().SetActiveReflector(Arg.Any<Guid?>());
    }

    [Fact]
    public async Task Handle_WhenReflectorIsDeleted_ShouldFail()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var aggregate = CreateValidAggregate(reflectorId);
        aggregate.Delete();
        var command = new ActivateReflectorCommand(reflectorId);

        _repository.GetByIdAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_DELETED");
        });
        _tracker.DidNotReceive().SetActiveReflector(Arg.Any<Guid?>());
    }

    [Fact]
    public async Task Handle_WhenConfigWriteFails_ShouldFail()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var aggregate = CreateValidAggregate(reflectorId);
        var command = new ActivateReflectorCommand(reflectorId);

        _repository.GetByIdAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());
        _configurationService.WriteConfigAsync(aggregate, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(
                global::LanguageExt.Common.Error.New("WRITE_ERROR")));

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_CONFIG_ERROR");
        });
        await _daemonService.DidNotReceive().RestartAsync(Arg.Any<CancellationToken>());
        _tracker.DidNotReceive().SetActiveReflector(Arg.Any<Guid?>());
    }

    [Fact]
    public async Task Handle_WhenDaemonRestartFails_ShouldFail()
    {
        // Arrange
        var reflectorId = Guid.NewGuid();
        var aggregate = CreateValidAggregate(reflectorId);
        var command = new ActivateReflectorCommand(reflectorId);

        _repository.GetByIdAsync(reflectorId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());
        _configurationService.WriteConfigAsync(aggregate, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(unit));
        _daemonService.RestartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<global::LanguageExt.Common.Error, Unit>>(
                global::LanguageExt.Common.Error.New("RESTART_ERROR")));

        // Act
        var result = await CallHandle(command);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_DAEMON_ERROR");
        });
        _tracker.DidNotReceive().SetActiveReflector(Arg.Any<Guid?>());
    }

    private Task<Validation<Error, Unit>> CallHandle(ActivateReflectorCommand command)
    {
        var handler = new ActivateReflectorCommandHandler(
            _repository, _tracker, _configurationService, _daemonService, _logger);
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
