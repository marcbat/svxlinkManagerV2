using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Entities;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil.Events;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.RadioProfil;

/// <summary>
/// Tests unitaires pour RadioProfilAggregate
/// </summary>
public class RadioProfilAggregateTests
{
    #region Factory Create Tests

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Profil VHF 144.800";
        var rxConfig = CreateValidRxConfiguration();
        var txConfig = CreateValidTxConfiguration();

        // Act
        var result = RadioProfilAggregate.Create(id, name, rxConfig, txConfig);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.Id.Should().Be(id);
            aggregate.Name.Should().Be(name);
            aggregate.RxConfiguration.Should().Be(rxConfig);
            aggregate.TxConfiguration.Should().Be(txConfig);
            aggregate.IsDeleted.Should().BeFalse();
            aggregate.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<RadioProfilCreatedEvent>();
        });
    }

    [Fact]
    public void Create_WithEmptyId_ShouldFail()
    {
        // Arrange
        var id = Guid.Empty;
        var name = "Profil Test";
        var rxConfig = CreateValidRxConfiguration();
        var txConfig = CreateValidTxConfiguration();

        // Act
        var result = RadioProfilAggregate.Create(id, name, rxConfig, txConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("EMPTY_ID"));
        });
    }

    [Fact]
    public void Create_WithEmptyName_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "";
        var rxConfig = CreateValidRxConfiguration();
        var txConfig = CreateValidTxConfiguration();

        // Act
        var result = RadioProfilAggregate.Create(id, name, rxConfig, txConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "RADIOPROFIL_NAME_REQUIRED");
        });
    }

    [Fact]
    public void Create_WithInvalidSqlDet_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Profil Test";
        var rxConfig = new RxConfiguration(
            Guid.NewGuid(),
            "Local",
            "alsa:plughw:0",
            0,
            "INVALID_SQLDET", // Invalid
            500,
            150,
            20,
            1000,
            null,
            15);
        var txConfig = CreateValidTxConfiguration();

        // Act
        var result = RadioProfilAggregate.Create(id, name, rxConfig, txConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "INVALID_SQL_DET");
        });
    }

    [Fact]
    public void Create_WithInvalidCtcssFq_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Profil Test";
        var rxConfig = new RxConfiguration(
            Guid.NewGuid(),
            "Local",
            "alsa:plughw:0",
            0,
            "GPIO",
            500,
            150,
            20,
            1000,
            500m, // Invalid: > 300 Hz
            15);
        var txConfig = CreateValidTxConfiguration();

        // Act
        var result = RadioProfilAggregate.Create(id, name, rxConfig, txConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "INVALID_CTCSS_FQ");
        });
    }

    [Fact]
    public void Create_WithInvalidAudioDev_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Profil Test";
        var rxConfig = new RxConfiguration(
            Guid.NewGuid(),
            "Local",
            "invalid_format", // Invalid: pas de ':'
            0,
            "GPIO",
            500,
            150,
            20,
            1000,
            null,
            15);
        var txConfig = CreateValidTxConfiguration();

        // Act
        var result = RadioProfilAggregate.Create(id, name, rxConfig, txConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "INVALID_AUDIO_DEV");
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidDelays_ShouldFail(int invalidDelay)
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Profil Test";
        var rxConfig = new RxConfiguration(
            Guid.NewGuid(),
            "Local",
            "alsa:plughw:0",
            0,
            "GPIO",
            invalidDelay, // Invalid
            150,
            20,
            1000,
            null,
            15);
        var txConfig = CreateValidTxConfiguration();

        // Act
        var result = RadioProfilAggregate.Create(id, name, rxConfig, txConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "INVALID_SQL_START_DELAY");
        });
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_WithValidName_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        var newName = "Nouveau Nom";

        // Act
        var result = aggregate.Update(name: newName);

        // Assert
        result.ShouldBeSuccess();
        aggregate.Name.Should().Be(newName);
        aggregate.DomainEvents.Should().Contain(e => e is RadioProfilUpdatedEvent);
    }

    [Fact]
    public void Update_WhenDeleted_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Delete();
        aggregate.ClearDomainEvents();

        // Act
        var result = aggregate.Update(name: "Test");

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "RADIOPROFIL_DELETED");
        });
    }

    [Fact]
    public void Update_WithInvalidName_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.Update(name: "");

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "RADIOPROFIL_NAME_REQUIRED");
        });
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_WhenNotDeleted_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.Delete();

        // Assert
        result.ShouldBeSuccess();
        aggregate.IsDeleted.Should().BeTrue();
        aggregate.DomainEvents.Should().Contain(e => e is RadioProfilDeletedEvent);
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
            errors.Should().Contain(e => e.Code == "RADIOPROFIL_ALREADY_DELETED");
        });
    }

    #endregion

    #region Event Sourcing Tests

    [Fact]
    public void Apply_RadioProfilCreatedEvent_ShouldReconstructState()
    {
        // Arrange
        var aggregate = new RadioProfilAggregate();
        var id = Guid.NewGuid();
        var name = "Profil Event Sourcing";
        var rxConfig = CreateValidRxConfiguration();
        var txConfig = CreateValidTxConfiguration();
        var @event = new RadioProfilCreatedEvent(id, name, rxConfig, txConfig);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.Id.Should().Be(id);
        aggregate.Name.Should().Be(name);
        aggregate.RxConfiguration.Should().Be(rxConfig);
        aggregate.TxConfiguration.Should().Be(txConfig);
        aggregate.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Apply_RadioProfilUpdatedEvent_ShouldUpdateState()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        var newName = "Nom Mis à Jour";
        var @event = new RadioProfilUpdatedEvent(aggregate.Id, name: newName);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.Name.Should().Be(newName);
    }

    [Fact]
    public void Apply_RadioProfilDeletedEvent_ShouldMarkAsDeleted()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        var @event = new RadioProfilDeletedEvent(aggregate.Id);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void EventSourcing_MultipleEvents_ShouldReconstructFullState()
    {
        // Arrange
        var aggregate = new RadioProfilAggregate();
        var id = Guid.NewGuid();
        var name = "Profil Initial";
        var rxConfig = CreateValidRxConfiguration();
        var txConfig = CreateValidTxConfiguration();

        var createdEvent = new RadioProfilCreatedEvent(id, name, rxConfig, txConfig);
        var updatedEvent = new RadioProfilUpdatedEvent(id, name: "Profil Modifié");
        var deletedEvent = new RadioProfilDeletedEvent(id);

        // Act - Rejouer les événements
        aggregate.Apply(createdEvent);
        aggregate.Apply(updatedEvent);
        aggregate.Apply(deletedEvent);

        // Assert
        aggregate.Id.Should().Be(id);
        aggregate.Name.Should().Be("Profil Modifié");
        aggregate.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static RxConfiguration CreateValidRxConfiguration()
    {
        return new RxConfiguration(
            Guid.NewGuid(),
            "Local",
            "alsa:plughw:0",
            0,
            "GPIO",
            500,
            150,
            20,
            1000,
            71.9m, // CTCSS valide
            15);
    }

    private static TxConfiguration CreateValidTxConfiguration()
    {
        return new TxConfiguration(
            Guid.NewGuid(),
            "Local",
            "alsa:plughw:0",
            0,
            900,
            0,
            71.9m, // CTCSS valide
            9,
            0,
            100,
            50,
            -15);
    }

    private static RadioProfilAggregate CreateValidAggregate()
    {
        var result = RadioProfilAggregate.Create(
            Guid.NewGuid(),
            "Profil Test",
            CreateValidRxConfiguration(),
            CreateValidTxConfiguration());

        return result.Match(
            Succ: aggregate => aggregate,
            Fail: errors => throw new InvalidOperationException($"Failed to create aggregate: {string.Join(", ", errors)}")
        );
    }

    #endregion
}
