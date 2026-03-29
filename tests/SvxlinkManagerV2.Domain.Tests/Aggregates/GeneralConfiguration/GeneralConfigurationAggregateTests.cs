using FluentAssertions;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Events;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.GeneralConfiguration;

/// <summary>
/// Tests unitaires pour GeneralConfigurationAggregate
/// </summary>
public class GeneralConfigurationAggregateTests
{
    #region Create Tests

    [Fact]
    public void Create_WithDefaultValues_ShouldSucceed()
    {
        // Act
        var result = GeneralConfigurationAggregate.Create();

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.Id.Should().Be(GeneralConfigurationAggregate.FixedId);
            aggregate.StartReflectorOnStartup.Should().BeFalse();
            aggregate.StartDefaultSalonOnStartup.Should().BeFalse();
            aggregate.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<GeneralConfigurationCreated>();
        });
    }

    [Fact]
    public void Create_WithStartReflectorTrue_ShouldSetStartReflector()
    {
        // Act
        var result = GeneralConfigurationAggregate.Create(startReflectorOnStartup: true);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.StartReflectorOnStartup.Should().BeTrue();
            aggregate.StartDefaultSalonOnStartup.Should().BeFalse();
        });
    }

    [Fact]
    public void Create_WithBothStartOptionsTrue_ShouldSetBothOptions()
    {
        // Act
        var result = GeneralConfigurationAggregate.Create(
            startReflectorOnStartup: true,
            startDefaultSalonOnStartup: true);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.StartReflectorOnStartup.Should().BeTrue();
            aggregate.StartDefaultSalonOnStartup.Should().BeTrue();
        });
    }

    [Fact]
    public void Create_ShouldEmitGeneralConfigurationCreatedEvent()
    {
        // Act
        var result = GeneralConfigurationAggregate.Create(
            startReflectorOnStartup: true,
            startDefaultSalonOnStartup: false);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            var evt = aggregate.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<GeneralConfigurationCreated>().Subject;

            evt.Id.Should().Be(GeneralConfigurationAggregate.FixedId);
            evt.StartReflectorOnStartup.Should().BeTrue();
            evt.StartDefaultSalonOnStartup.Should().BeFalse();
        });
    }

    [Fact]
    public void Create_ShouldAlwaysUseFixedId()
    {
        // Act
        var result1 = GeneralConfigurationAggregate.Create();
        var result2 = GeneralConfigurationAggregate.Create();

        // Assert
        result1.ShouldBeSuccess(a1 =>
        {
            result2.ShouldBeSuccess(a2 =>
            {
                a1.Id.Should().Be(a2.Id);
                a1.Id.Should().Be(GeneralConfigurationAggregate.FixedId);
            });
        });
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_ShouldChangeStartOptions()
    {
        // Arrange
        var aggregate = CreateValidAggregate(false, false);

        // Act
        var result = aggregate.Update(
            startReflectorOnStartup: true,
            startDefaultSalonOnStartup: true);

        // Assert
        result.ShouldBeSuccess();
        aggregate.StartReflectorOnStartup.Should().BeTrue();
        aggregate.StartDefaultSalonOnStartup.Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldEmitGeneralConfigurationUpdatedEvent()
    {
        // Arrange
        var aggregate = CreateValidAggregate(false, false);
        aggregate.ClearDomainEvents();

        // Act
        var result = aggregate.Update(
            startReflectorOnStartup: true,
            startDefaultSalonOnStartup: false);

        // Assert
        result.ShouldBeSuccess();
        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GeneralConfigurationUpdated>();
    }

    [Fact]
    public void Update_WithSameValues_ShouldStillEmitEvent()
    {
        // Arrange
        var aggregate = CreateValidAggregate(true, true);
        aggregate.ClearDomainEvents();

        // Act
        var result = aggregate.Update(
            startReflectorOnStartup: true,
            startDefaultSalonOnStartup: true);

        // Assert
        result.ShouldBeSuccess();
        aggregate.StartReflectorOnStartup.Should().BeTrue();
        aggregate.StartDefaultSalonOnStartup.Should().BeTrue();
        aggregate.DomainEvents.Should().ContainSingle();
    }

    #endregion

    #region Event Sourcing Apply Tests

    [Fact]
    public void Apply_GeneralConfigurationCreated_ShouldSetInitialState()
    {
        // Arrange
        var aggregate = new GeneralConfigurationAggregate();
        var evt = new GeneralConfigurationCreated(
            GeneralConfigurationAggregate.FixedId,
            startReflectorOnStartup: true,
            startDefaultSalonOnStartup: true);

        // Act
        aggregate.Apply(evt);

        // Assert
        aggregate.Id.Should().Be(GeneralConfigurationAggregate.FixedId);
        aggregate.StartReflectorOnStartup.Should().BeTrue();
        aggregate.StartDefaultSalonOnStartup.Should().BeTrue();
    }

    [Fact]
    public void Apply_GeneralConfigurationUpdated_ShouldUpdateState()
    {
        // Arrange
        var aggregate = CreateValidAggregate(false, false);
        var evt = new GeneralConfigurationUpdated(
            startReflectorOnStartup: true,
            startDefaultSalonOnStartup: true);

        // Act
        aggregate.Apply(evt);

        // Assert
        aggregate.StartReflectorOnStartup.Should().BeTrue();
        aggregate.StartDefaultSalonOnStartup.Should().BeTrue();
    }

    #endregion

    #region FixedId Tests

    [Fact]
    public void FixedId_ShouldBeConsistentConstant()
    {
        // Assert
        GeneralConfigurationAggregate.FixedId.Should().NotBe(Guid.Empty);
        GeneralConfigurationAggregate.FixedId.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    }

    #endregion

    #region Helpers

    private static GeneralConfigurationAggregate CreateValidAggregate(
        bool startReflectorOnStartup,
        bool startDefaultSalonOnStartup)
    {
        var result = GeneralConfigurationAggregate.Create(
            startReflectorOnStartup,
            startDefaultSalonOnStartup);

        return result.Match(
            Succ: a =>
            {
                a.ClearDomainEvents();
                return a;
            },
            Fail: _ => throw new InvalidOperationException("La création de l'aggregate de test a échoué"));
    }

    #endregion
}
