using FluentAssertions;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.Reflector;

/// <summary>
/// Tests unitaires pour ReflectorAggregate
/// </summary>
public class ReflectorAggregateTests
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

    #region Factory Create Tests

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "SvxReflector Local";

        // Act
        var result = ReflectorAggregate.Create(id, name, ValidConfig);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.Id.Should().Be(id);
            aggregate.Name.Should().Be(name);
            aggregate.Config.Should().Be(ValidConfig);
            aggregate.IsActive.Should().BeFalse();
            aggregate.IsDeleted.Should().BeFalse();
            aggregate.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<ReflectorCreated>();
        });
    }

    [Fact]
    public void Create_WithEmptyId_ShouldFail()
    {
        // Arrange
        var id = Guid.Empty;

        // Act
        var result = ReflectorAggregate.Create(id, "Reflector Test", ValidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("EMPTY_ID"));
        });
    }

    [Fact]
    public void Create_WithEmptyName_ShouldFail()
    {
        // Act
        var result = ReflectorAggregate.Create(Guid.NewGuid(), "", ValidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_NAME_REQUIRED");
        });
    }

    [Fact]
    public void Create_WithWhitespaceName_ShouldFail()
    {
        // Act
        var result = ReflectorAggregate.Create(Guid.NewGuid(), "   ", ValidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_NAME_REQUIRED");
        });
    }

    [Fact]
    public void Create_WithEmptyConfig_ShouldFail()
    {
        // Act
        var result = ReflectorAggregate.Create(Guid.NewGuid(), "Reflector Test", "");

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_CONFIG_REQUIRED");
        });
    }

    [Fact]
    public void Create_WithConfigMissingGlobal_ShouldFail()
    {
        // Arrange
        var configWithoutGlobal = """
            [USERS]
            HB9GXP-H=DevNodes
            """;

        // Act
        var result = ReflectorAggregate.Create(Guid.NewGuid(), "Reflector Test", configWithoutGlobal);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_CONFIG_INVALID");
        });
    }

    [Fact]
    public void Create_ShouldEmitReflectorCreatedEvent()
    {
        // Act
        var result = ReflectorAggregate.Create(Guid.NewGuid(), "Reflector Test", ValidConfig);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            var evt = aggregate.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<ReflectorCreated>().Subject;

            evt.Name.Should().Be("Reflector Test");
            evt.Config.Should().Be(ValidConfig);
        });
    }

    #endregion

    #region UpdateConfiguration Tests

    [Fact]
    public void UpdateConfiguration_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        var newConfig = ValidConfig.Replace("5300", "5400");

        // Act
        var result = aggregate.UpdateConfiguration("Nouveau Nom", newConfig);

        // Assert
        result.ShouldBeSuccess();
        aggregate.Name.Should().Be("Nouveau Nom");
        aggregate.Config.Should().Be(newConfig);
        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReflectorConfigurationUpdated>();
    }

    [Fact]
    public void UpdateConfiguration_WhenActive_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Activate();
        aggregate.ClearDomainEvents();

        // Act
        var result = aggregate.UpdateConfiguration("Nouveau Nom", ValidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_ACTIVE");
        });
    }

    [Fact]
    public void UpdateConfiguration_WhenDeleted_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Delete();
        aggregate.ClearDomainEvents();

        // Act
        var result = aggregate.UpdateConfiguration("Nouveau Nom", ValidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_DELETED");
        });
    }

    [Fact]
    public void UpdateConfiguration_WithEmptyName_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.UpdateConfiguration("", ValidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_NAME_REQUIRED");
        });
    }

    [Fact]
    public void UpdateConfiguration_WithInvalidConfig_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.UpdateConfiguration("Nom", "config sans section GLOBAL");

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_CONFIG_INVALID");
        });
    }

    #endregion

    #region Activate Tests

    [Fact]
    public void Activate_WhenInactive_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.Activate();

        // Assert
        result.ShouldBeSuccess();
        aggregate.IsActive.Should().BeTrue();
        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReflectorActivated>();
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Activate();
        aggregate.ClearDomainEvents();

        // Act
        var result = aggregate.Activate();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_ALREADY_ACTIVE");
        });
    }

    [Fact]
    public void Activate_WhenDeleted_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Delete();
        aggregate.ClearDomainEvents();

        // Act
        var result = aggregate.Activate();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_DELETED");
        });
    }

    #endregion

    #region Deactivate Tests

    [Fact]
    public void Deactivate_WhenActive_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Activate();
        aggregate.ClearDomainEvents();

        // Act
        var result = aggregate.Deactivate();

        // Assert
        result.ShouldBeSuccess();
        aggregate.IsActive.Should().BeFalse();
        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReflectorDeactivated>();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.Deactivate();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_ALREADY_INACTIVE");
        });
    }

    [Fact]
    public void Deactivate_WhenDeleted_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Delete();
        aggregate.ClearDomainEvents();

        // Act
        var result = aggregate.Deactivate();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_DELETED");
        });
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_WhenInactive_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.Delete();

        // Assert
        result.ShouldBeSuccess();
        aggregate.IsDeleted.Should().BeTrue();
        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReflectorDeleted>();
    }

    [Fact]
    public void Delete_WhenActive_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Activate();
        aggregate.ClearDomainEvents();

        // Act
        var result = aggregate.Delete();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_ACTIVE");
        });
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Delete();
        aggregate.ClearDomainEvents();

        // Act
        var result = aggregate.Delete();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "REFLECTOR_ALREADY_DELETED");
        });
    }

    #endregion

    #region Event Sourcing Apply Tests

    [Fact]
    public void Apply_ReflectorCreated_ShouldSetInitialState()
    {
        // Arrange
        var id = Guid.NewGuid();
        var result = ReflectorAggregate.Create(id, "SvxReflector Local", ValidConfig);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.Id.Should().Be(id);
            aggregate.Name.Should().Be("SvxReflector Local");
            aggregate.Config.Should().Be(ValidConfig);
            aggregate.IsActive.Should().BeFalse();
            aggregate.IsDeleted.Should().BeFalse();
        });
    }

    [Fact]
    public void Apply_MultipleEvents_ShouldReplayCorrectly()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act - Simulate lifecycle
        aggregate.Activate();
        aggregate.Deactivate();
        aggregate.UpdateConfiguration("Nom Mis à Jour", ValidConfig);
        aggregate.Activate();
        aggregate.Deactivate();
        aggregate.Delete();

        // Assert final state
        aggregate.IsActive.Should().BeFalse();
        aggregate.IsDeleted.Should().BeTrue();
        aggregate.Name.Should().Be("Nom Mis à Jour");
        aggregate.DomainEvents.Should().HaveCount(6);
    }

    #endregion

    #region Helpers

    private static ReflectorAggregate CreateValidAggregate()
    {
        var result = ReflectorAggregate.Create(Guid.NewGuid(), "SvxReflector Test", ValidConfig);
        return result.Match(
            Succ: a =>
            {
                a.ClearDomainEvents();
                return a;
            },
            Fail: _ => throw new InvalidOperationException("La création de l'aggregate test a échoué"));
    }

    #endregion
}
