using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.GeneralConfiguration.CreateOrUpdate;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Tests.Features.GeneralConfiguration;

/// <summary>
/// Tests unitaires pour CreateOrUpdateGeneralConfigurationCommand et son handler.
/// </summary>
public class CreateOrUpdateGeneralConfigurationCommandTests
{
    private readonly IGeneralConfigurationRepository _repository;
    private readonly ILogger<CreateOrUpdateGeneralConfigurationCommandHandler> _logger;

    public CreateOrUpdateGeneralConfigurationCommandTests()
    {
        _repository = Substitute.For<IGeneralConfigurationRepository>();
        _logger = Substitute.For<ILogger<CreateOrUpdateGeneralConfigurationCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WhenNoExistingConfiguration_ShouldCreateNewOne()
    {
        // Arrange
        var command = new CreateOrUpdateGeneralConfigurationCommand(
            StartReflectorOnStartup: true,
            StartDefaultSalonOnStartup: false);

        _repository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _repository.SaveAsync(Arg.Any<GeneralConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await new CreateOrUpdateGeneralConfigurationCommandHandler(_repository, _logger)
            .Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();

        await _repository.Received(1).SaveAsync(
            Arg.Is<GeneralConfigurationAggregate>(a =>
                a.StartReflectorOnStartup == true &&
                a.StartDefaultSalonOnStartup == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExistingConfiguration_ShouldUpdateIt()
    {
        // Arrange
        var existing = CreateValidAggregate(false, false);
        var command = new CreateOrUpdateGeneralConfigurationCommand(
            StartReflectorOnStartup: true,
            StartDefaultSalonOnStartup: true);

        _repository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(existing);
        _repository.SaveAsync(Arg.Any<GeneralConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        // Act
        var result = await new CreateOrUpdateGeneralConfigurationCommandHandler(_repository, _logger)
            .Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        existing.StartReflectorOnStartup.Should().BeTrue();
        existing.StartDefaultSalonOnStartup.Should().BeTrue();

        await _repository.Received(1).SaveAsync(
            Arg.Is<GeneralConfigurationAggregate>(a =>
                a.StartReflectorOnStartup == true &&
                a.StartDefaultSalonOnStartup == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRepositorySaveFails_ShouldFail()
    {
        // Arrange
        var command = new CreateOrUpdateGeneralConfigurationCommand(
            StartReflectorOnStartup: true,
            StartDefaultSalonOnStartup: false);

        _repository.GetAsync(Arg.Any<CancellationToken>())
            .Returns((GeneralConfigurationAggregate?)null);
        _repository.SaveAsync(Arg.Any<GeneralConfigurationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation("SAVE_ERROR", "Erreur lors de la sauvegarde").ToFailure<Unit>());

        // Act
        var result = await new CreateOrUpdateGeneralConfigurationCommandHandler(_repository, _logger)
            .Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SAVE_ERROR");
        });
    }

    private static GeneralConfigurationAggregate CreateValidAggregate(
        bool startReflectorOnStartup,
        bool startDefaultSalonOnStartup)
    {
        var result = GeneralConfigurationAggregate.Create(
            startReflectorOnStartup,
            startDefaultSalonOnStartup);

        return result.Match(
            Succ: a => { a.ClearDomainEvents(); return a; },
            Fail: _ => throw new InvalidOperationException("Failed to create test aggregate"));
    }
}
