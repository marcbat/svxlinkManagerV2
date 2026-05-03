using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.ActivateStandaloneMode;
using SvxlinkManagerV2.Application.Features.Salons.DeactivateSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour DeactivateSalonCommand et son handler.
/// La désactivation d'un salon repositionne SVXLink en mode standalone
/// (simplex sans réflecteur, écoute DTMF active) via ActivateStandaloneModeCommand.
/// </summary>
public class DeactivateSalonCommandTests
{
    private readonly IActiveSessionTracker _tracker;
    private readonly IMediator _mediator;
    private readonly ILogger<DeactivateSalonCommandHandler> _logger;

    public DeactivateSalonCommandTests()
    {
        _tracker = Substitute.For<IActiveSessionTracker>();
        _mediator = Substitute.For<IMediator>();
        _logger = Substitute.For<ILogger<DeactivateSalonCommandHandler>>();
    }

    private DeactivateSalonCommandHandler CreateHandler() =>
        new(_tracker, _mediator, _logger);

    [Fact]
    public async Task Handle_WhenSalonIsActive_ShouldActivateStandaloneMode()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var command = new DeactivateSalonCommand(salonId);

        _tracker.IsSalonActive(salonId).Returns(true);
        _mediator.Send(Arg.Any<ActivateStandaloneModeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(unit.ToSuccess()));

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        await _mediator.Received(1).Send(Arg.Any<ActivateStandaloneModeCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSalonNotActive_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var command = new DeactivateSalonCommand(salonId);

        _tracker.IsSalonActive(salonId).Returns(false);

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_NOT_ACTIVE");
        });

        await _mediator.DidNotReceive().Send(Arg.Any<ActivateStandaloneModeCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenStandaloneActivationFails_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var command = new DeactivateSalonCommand(salonId);

        _tracker.IsSalonActive(salonId).Returns(true);
        _mediator.Send(Arg.Any<ActivateStandaloneModeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Error.Validation("STANDALONE_FAIL", "Échec du mode standalone").ToFailure<Unit>()));

        // Act
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "STANDALONE_ACTIVATION_ERROR");
        });
    }
}
