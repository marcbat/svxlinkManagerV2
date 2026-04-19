using FluentAssertions;
using LanguageExt.UnitTesting;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.Salon;

/// <summary>
/// Tests unitaires pour SalonAggregate
/// </summary>
public class SalonAggregateTests
{
    #region Factory Create Tests

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon National France";
        var config = CreateValidConfiguration();

        // Act
        var result = SalonAggregate.Create(id, name, isDefault: true, isTemporized: false, config);

        // Assert
        result.ShouldBeSuccess(aggregate =>
        {
            aggregate.Id.Should().Be(id);
            aggregate.Name.Should().Be(name);
            aggregate.IsDefault.Should().BeTrue();
            aggregate.IsTemporized.Should().BeFalse();
            aggregate.IsDeleted.Should().BeFalse();
            aggregate.Configuration.Should().Be(config);
            aggregate.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<SalonCreated>();
        });
    }

    [Fact]
    public void Create_WithEmptyId_ShouldFail()
    {
        // Arrange
        var id = Guid.Empty;
        var name = "Salon Test";
        var config = CreateValidConfiguration();

        // Act
        var result = SalonAggregate.Create(id, name, false, false, config);

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
        var config = CreateValidConfiguration();

        // Act
        var result = SalonAggregate.Create(id, name, false, false, config);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_NAME_REQUIRED");
        });
    }

    [Fact]
    public void Create_WithEmptyHost_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon Test";
        var config = CreateValidConfiguration();
        var invalidConfig = new SvxLinkConfiguration(
            config.Id,
            config.Logics,
            config.CfgDir,
            config.CardSampleRate,
            config.CardChannels,
            "", // Host vide
            config.Port,
            config.Callsign,
            config.AuthKey,
            config.JitterBufferDelay,
            config.ReflectorProtocol,
            config.CertEmail,
            config.SimplexCallsign,
            config.Modules,
            config.ShortIdentInterval,
            config.LongIdentInterval,
            config.ReportCtcss,
            config.DefaultLang,
            config.RgrSoundDelay,
            config.RxFrequency,
            config.TxFrequency,
            config.RxCtcss,
            config.TxCtcss);

        // Act
        var result = SalonAggregate.Create(id, name, false, false, invalidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_HOST_REQUIRED");
        });
    }

    [Fact]
    public void Create_WithInvalidHostFormat_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon Test";
        var config = CreateValidConfiguration();
        var invalidConfig = new SvxLinkConfiguration(
            config.Id,
            config.Logics,
            config.CfgDir,
            config.CardSampleRate,
            config.CardChannels,
            "invalid host!", // Format invalide
            config.Port,
            config.Callsign,
            config.AuthKey,
            config.JitterBufferDelay,
            config.ReflectorProtocol,
            config.CertEmail,
            config.SimplexCallsign,
            config.Modules,
            config.ShortIdentInterval,
            config.LongIdentInterval,
            config.ReportCtcss,
            config.DefaultLang,
            config.RgrSoundDelay,
            config.RxFrequency,
            config.TxFrequency,
            config.RxCtcss,
            config.TxCtcss);

        // Act
        var result = SalonAggregate.Create(id, name, false, false, invalidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_HOST_INVALID");
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void Create_WithInvalidPort_ShouldFail(int invalidPort)
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon Test";
        var config = CreateValidConfiguration();
        var invalidConfig = new SvxLinkConfiguration(
            config.Id,
            config.Logics,
            config.CfgDir,
            config.CardSampleRate,
            config.CardChannels,
            config.Host,
            invalidPort, // Port invalide
            config.Callsign,
            config.AuthKey,
            config.JitterBufferDelay,
            config.ReflectorProtocol,
            config.CertEmail,
            config.SimplexCallsign,
            config.Modules,
            config.ShortIdentInterval,
            config.LongIdentInterval,
            config.ReportCtcss,
            config.DefaultLang,
            config.RgrSoundDelay,
            config.RxFrequency,
            config.TxFrequency,
            config.RxCtcss,
            config.TxCtcss);

        // Act
        var result = SalonAggregate.Create(id, name, false, false, invalidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_PORT_INVALID");
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyCallsign_ShouldFail(string invalidCallsign)
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon Test";
        var config = CreateValidConfiguration();
        var invalidConfig = new SvxLinkConfiguration(
            config.Id,
            config.Logics,
            config.CfgDir,
            config.CardSampleRate,
            config.CardChannels,
            config.Host,
            config.Port,
            invalidCallsign, // Callsign vide
            config.AuthKey,
            config.JitterBufferDelay,
            config.ReflectorProtocol,
            config.CertEmail,
            config.SimplexCallsign,
            config.Modules,
            config.ShortIdentInterval,
            config.LongIdentInterval,
            config.ReportCtcss,
            config.DefaultLang,
            config.RgrSoundDelay,
            config.RxFrequency,
            config.TxFrequency,
            config.RxCtcss,
            config.TxCtcss);

        // Act
        var result = SalonAggregate.Create(id, name, false, false, invalidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_CALLSIGN_REQUIRED");
        });
    }

    [Fact]
    public void Create_WithEmptyAuthKey_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon Test";
        var config = CreateValidConfiguration();
        var invalidConfig = new SvxLinkConfiguration(
            config.Id,
            config.Logics,
            config.CfgDir,
            config.CardSampleRate,
            config.CardChannels,
            config.Host,
            config.Port,
            config.Callsign,
            "", // AuthKey vide
            config.JitterBufferDelay,
            config.ReflectorProtocol,
            config.CertEmail,
            config.SimplexCallsign,
            config.Modules,
            config.ShortIdentInterval,
            config.LongIdentInterval,
            config.ReportCtcss,
            config.DefaultLang,
            config.RgrSoundDelay,
            config.RxFrequency,
            config.TxFrequency,
            config.RxCtcss,
            config.TxCtcss);

        // Act
        var result = SalonAggregate.Create(id, name, false, false, invalidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_AUTHKEY_REQUIRED");
        });
    }

    [Fact]
    public void Create_WithV3TalkGroupConfiguration_ShouldSucceed()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon V3";
        var config = CreateValidConfiguration() with
        {
            ReflectorProtocol = ReflectorProtocol.V3,
            AuthKey = null,
            DefaultTg = 208,
            MonitorTgs = "91,208,226+,228+",
            TgSelectTimeout = 45,
            TgSelectInhibitTimeout = 10,
            TmpMonitorTimeout = 1200,
            QsyPendingTimeout = -1
        };

        // Act
        var result = SalonAggregate.Create(id, name, false, false, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithInvalidV3MonitorTgs_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon V3";
        var config = CreateValidConfiguration() with
        {
            ReflectorProtocol = ReflectorProtocol.V3,
            AuthKey = null,
            MonitorTgs = "91,abc"
        };

        // Act
        var result = SalonAggregate.Create(id, name, false, false, config);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_MONITOR_TGS_INVALID");
        });
    }

    [Fact]
    public void Create_WithInvalidV3TgSelectTimeout_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon V3";
        var config = CreateValidConfiguration() with
        {
            ReflectorProtocol = ReflectorProtocol.V3,
            AuthKey = null,
            TgSelectTimeout = 0
        };

        // Act
        var result = SalonAggregate.Create(id, name, false, false, config);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_TG_SELECT_TIMEOUT_INVALID");
        });
    }

    [Fact]
    public void Create_WithInvalidTalkGroupValuesInV2_ShouldIgnoreTalkGroupValidation()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon V2";
        var config = CreateValidConfiguration() with
        {
            ReflectorProtocol = ReflectorProtocol.V2,
            DefaultTg = -1,
            MonitorTgs = "bad-value",
            TgSelectTimeout = 0,
            TgSelectInhibitTimeout = -1,
            TmpMonitorTimeout = -1,
            QsyPendingTimeout = -2
        };

        // Act
        var result = SalonAggregate.Create(id, name, false, false, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

        [Fact]
    public void Create_WithInvalidRxFrequency_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon Test";
        var config = CreateValidConfiguration();
        var invalidConfig = new SvxLinkConfiguration(
            config.Id,
            config.Logics,
            config.CfgDir,
            config.CardSampleRate,
            config.CardChannels,
            config.Host,
            config.Port,
            config.Callsign,
            config.AuthKey,
            config.JitterBufferDelay,
            config.ReflectorProtocol,
            config.CertEmail,
            config.SimplexCallsign,
            config.Modules,
            config.ShortIdentInterval,
            config.LongIdentInterval,
            config.ReportCtcss,
            config.DefaultLang,
            config.RgrSoundDelay,
            0m, // RxFrequency invalide (hors plage 30-3000 MHz)
            config.TxFrequency,
            config.RxCtcss,
            config.TxCtcss);

        // Act
        var result = SalonAggregate.Create(id, name, false, false, invalidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_RXFREQUENCY_INVALID");
        });
    }

    [Fact]
    public void Create_WithInvalidTxFrequency_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon Test";
        var config = CreateValidConfiguration();
        var invalidConfig = new SvxLinkConfiguration(
            config.Id,
            config.Logics,
            config.CfgDir,
            config.CardSampleRate,
            config.CardChannels,
            config.Host,
            config.Port,
            config.Callsign,
            config.AuthKey,
            config.JitterBufferDelay,
            config.ReflectorProtocol,
            config.CertEmail,
            config.SimplexCallsign,
            config.Modules,
            config.ShortIdentInterval,
            config.LongIdentInterval,
            config.ReportCtcss,
            config.DefaultLang,
            config.RgrSoundDelay,
            config.RxFrequency,
            4000m, // TxFrequency invalide (hors plage 30-3000 MHz)
            config.RxCtcss,
            config.TxCtcss);

        // Act
        var result = SalonAggregate.Create(id, name, false, false, invalidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_TXFREQUENCY_INVALID");
        });
    }

    [Fact]
    public void Create_WithInvalidRxCtcss_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon Test";
        var config = CreateValidConfiguration();
        var invalidConfig = new SvxLinkConfiguration(
            config.Id,
            config.Logics,
            config.CfgDir,
            config.CardSampleRate,
            config.CardChannels,
            config.Host,
            config.Port,
            config.Callsign,
            config.AuthKey,
            config.JitterBufferDelay,
            config.ReflectorProtocol,
            config.CertEmail,
            config.SimplexCallsign,
            config.Modules,
            config.ShortIdentInterval,
            config.LongIdentInterval,
            config.ReportCtcss,
            config.DefaultLang,
            config.RgrSoundDelay,
            config.RxFrequency,
            config.TxFrequency,
            300m, // RxCtcss invalide (hors plage 67.0-250.3 Hz)
            config.TxCtcss);

        // Act
        var result = SalonAggregate.Create(id, name, false, false, invalidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_RXCTCSS_INVALID");
        });
    }

    [Fact]
    public void Create_WithInvalidTxCtcss_ShouldFail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon Test";
        var config = CreateValidConfiguration();
        var invalidConfig = new SvxLinkConfiguration(
            config.Id,
            config.Logics,
            config.CfgDir,
            config.CardSampleRate,
            config.CardChannels,
            config.Host,
            config.Port,
            config.Callsign,
            config.AuthKey,
            config.JitterBufferDelay,
            config.ReflectorProtocol,
            config.CertEmail,
            config.SimplexCallsign,
            config.Modules,
            config.ShortIdentInterval,
            config.LongIdentInterval,
            config.ReportCtcss,
            config.DefaultLang,
            config.RgrSoundDelay,
            config.RxFrequency,
            config.TxFrequency,
            config.RxCtcss,
            50m); // TxCtcss invalide (hors plage 67.0-250.3 Hz)

        // Act
        var result = SalonAggregate.Create(id, name, false, false, invalidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_TXCTCSS_INVALID");
        });
    }

    [Theory]
    [InlineData(4000)]
    [InlineData(11000)]
    [InlineData(32000)]
    public void Create_WithInvalidSampleRate_ShouldFail(int invalidRate)
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Salon Test";
        var config = CreateValidConfiguration();
        var invalidConfig = new SvxLinkConfiguration(
            config.Id,
            config.Logics,
            config.CfgDir,
            invalidRate, // Taux d'échantillonnage invalide
            config.CardChannels,
            config.Host,
            config.Port,
            config.Callsign,
            config.AuthKey,
            config.JitterBufferDelay,
            config.ReflectorProtocol,
            config.CertEmail,
            config.SimplexCallsign,
            config.Modules,
            config.ShortIdentInterval,
            config.LongIdentInterval,
            config.ReportCtcss,
            config.DefaultLang,
            config.RgrSoundDelay,
            config.RxFrequency,
            config.TxFrequency,
            config.RxCtcss,
            config.TxCtcss);

        // Act
        var result = SalonAggregate.Create(id, name, false, false, invalidConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_SAMPLERATE_INVALID");
        });
    }

    #endregion

    #region Update Configuration Tests

    [Fact]
    public void UpdateConfiguration_WithValidConfig_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        var newConfig = CreateValidConfiguration();

        // Act
        var result = aggregate.UpdateConfiguration(newConfig);

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.Configuration.Should().Be(newConfig);
            aggregate.DomainEvents.Should().HaveCount(2);
            aggregate.DomainEvents.Last().Should().BeOfType<SalonConfigurationUpdated>();
        });
    }

    [Fact]
    public void UpdateConfiguration_WhenDeleted_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Delete();
        var newConfig = CreateValidConfiguration();

        // Act
        var result = aggregate.UpdateConfiguration(newConfig);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_DELETED");
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
        result.ShouldBeSuccess(_ =>
        {
            aggregate.IsDeleted.Should().BeTrue();
            aggregate.DomainEvents.Last().Should().BeOfType<SalonDeleted>();
        });
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Delete();

        // Act
        var result = aggregate.Delete();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_ALREADY_DELETED");
        });
    }

    [Fact]
    public void Delete_WhenIsDefault_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.SetAsDefault();

        // Act
        var result = aggregate.Delete();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_IS_DEFAULT");
        });
    }

    #endregion

    #region Event Sourcing Tests

    [Fact]
    public void Apply_SalonCreated_ShouldSetProperties()
    {
        // Arrange
        var aggregate = new SalonAggregate();
        var id = Guid.NewGuid();
        var config = CreateValidConfiguration();
        var @event = new SalonCreated(id, "Salon Test", true, false, config);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.Id.Should().Be(id);
        aggregate.Name.Should().Be("Salon Test");
        aggregate.IsDefault.Should().BeTrue();
        aggregate.IsTemporized.Should().BeFalse();
        aggregate.IsDeleted.Should().BeFalse();
        aggregate.Configuration.Should().Be(config);
    }

    [Fact]
    public void Apply_SalonConfigurationUpdated_ShouldUpdateConfiguration()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        var newConfig = CreateValidConfiguration();
        var @event = new SalonConfigurationUpdated(aggregate.Id, newConfig);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.Configuration.Should().Be(newConfig);
    }

    [Fact]
    public void Apply_SalonDeleted_ShouldSetDeleted()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        var @event = new SalonDeleted(aggregate.Id);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void EventSourcing_ReplayMultipleEvents_ShouldReconstructState()
    {
        // Arrange
        var aggregate = new SalonAggregate();
        var id = Guid.NewGuid();
        var config = CreateValidConfiguration();

        var createdEvent = new SalonCreated(id, "Salon Initial", true, false, config);
        var updatedEvent = new SalonConfigurationUpdated(id, CreateValidConfiguration());

        // Act - Rejouer les événements
        aggregate.Apply(createdEvent);
        aggregate.Apply(updatedEvent);

        // Assert
        aggregate.Id.Should().Be(id);
        aggregate.Name.Should().Be("Salon Initial");
        aggregate.IsDeleted.Should().BeFalse();
    }

    #endregion

    #region SetAsDefault / UnsetDefault Tests

    [Fact]
    public void SetAsDefault_WhenNotDefault_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate(); // IsDefault = false par défaut

        // Act
        var result = aggregate.SetAsDefault();

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.IsDefault.Should().BeTrue();
            aggregate.DomainEvents.Last().Should().BeOfType<SalonSetAsDefault>();
        });
    }

    [Fact]
    public void SetAsDefault_WhenAlreadyDefault_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.SetAsDefault();

        // Act
        var result = aggregate.SetAsDefault();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_ALREADY_DEFAULT");
        });
    }

    [Fact]
    public void SetAsDefault_WhenDeleted_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Delete();

        // Act
        var result = aggregate.SetAsDefault();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_DELETED");
        });
    }

    [Fact]
    public void UnsetDefault_WhenIsDefault_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.SetAsDefault();

        // Act
        var result = aggregate.UnsetDefault();

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.IsDefault.Should().BeFalse();
            aggregate.DomainEvents.Last().Should().BeOfType<SalonUnsetDefault>();
        });
    }

    [Fact]
    public void UnsetDefault_WhenNotDefault_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate(); // IsDefault = false

        // Act
        var result = aggregate.UnsetDefault();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_NOT_DEFAULT");
        });
    }

    [Fact]
    public void UnsetDefault_WhenDeleted_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.SetAsDefault();
        aggregate.UnsetDefault(); // Remettre à false pour pouvoir supprimer
        aggregate.Delete();

        // Act
        var result = aggregate.UnsetDefault();

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_DELETED");
        });
    }

    [Fact]
    public void EventSourcing_Apply_SalonSetAsDefault_ShouldSetIsDefaultTrue()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        var @event = new SalonSetAsDefault(aggregate.Id);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void EventSourcing_Apply_SalonUnsetDefault_ShouldSetIsDefaultFalse()
    {
        // Arrange
        var aggregate = new SalonAggregate();
        var id = Guid.NewGuid();
        var config = CreateValidConfiguration();
        aggregate.Apply(new SalonCreated(id, "Salon Test", isDefault: true, isTemporized: false, config));

        var @event = new SalonUnsetDefault(id);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.IsDefault.Should().BeFalse();
    }

    #endregion

    #region DtmfCode Tests

    [Fact]
    public void UpdateDtmfCode_WithValidCode_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.UpdateDtmfCode(96);

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.DtmfCode.Should().Be(96);
            aggregate.DomainEvents.Last().Should().BeOfType<SalonDtmfCodeUpdated>();
        });
    }

    [Fact]
    public void UpdateDtmfCode_WithNull_ShouldClearCode()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.UpdateDtmfCode(96);

        // Act
        var result = aggregate.UpdateDtmfCode(null);

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.DtmfCode.Should().BeNull();
        });
    }

    [Fact]
    public void UpdateDtmfCode_WithMinValue_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.UpdateDtmfCode(1);

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.DtmfCode.Should().Be(1);
        });
    }

    [Fact]
    public void UpdateDtmfCode_WithMaxValue_ShouldSucceed()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.UpdateDtmfCode(9999);

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.DtmfCode.Should().Be(9999);
        });
    }

    [Fact]
    public void UpdateDtmfCode_WithZero_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.UpdateDtmfCode(0);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "DTMF_CODE_INVALID");
        });
    }

    [Fact]
    public void UpdateDtmfCode_WithNegativeValue_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.UpdateDtmfCode(-1);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "DTMF_CODE_INVALID");
        });
    }

    [Fact]
    public void UpdateDtmfCode_WithValueAbove9999_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();

        // Act
        var result = aggregate.UpdateDtmfCode(10000);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "DTMF_CODE_INVALID");
        });
    }

    [Fact]
    public void UpdateDtmfCode_WhenDeleted_ShouldFail()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Delete();

        // Act
        var result = aggregate.UpdateDtmfCode(96);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "SALON_DELETED");
        });
    }

    [Fact]
    public void Apply_SalonDtmfCodeUpdated_ShouldSetDtmfCode()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        var @event = new SalonDtmfCodeUpdated(aggregate.Id, 42);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.DtmfCode.Should().Be(42);
    }

    [Fact]
    public void Apply_SalonDtmfCodeUpdated_WithNull_ShouldClearDtmfCode()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.Apply(new SalonDtmfCodeUpdated(aggregate.Id, 42));

        var @event = new SalonDtmfCodeUpdated(aggregate.Id, null);

        // Act
        aggregate.Apply(@event);

        // Assert
        aggregate.DtmfCode.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldHaveNullDtmfCodeByDefault()
    {
        // Arrange & Act
        var aggregate = CreateValidAggregate();

        // Assert
        aggregate.DtmfCode.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static SvxLinkConfiguration CreateValidConfiguration()
    {
        return new SvxLinkConfiguration(
            Guid.NewGuid(),
            // Section GLOBAL
            Logics: "SimplexLogic,ReflectorLogic",
            CfgDir: "svxlink.d",
            CardSampleRate: 16000,
            CardChannels: 1,
            // Section ReflectorLogic
            Host: "ref.f5kri.fr",
            Port: 5300,
            Callsign: "F5ABC-L",
            AuthKey: "test-auth-key-123",
            JitterBufferDelay: 0,
            ReflectorProtocol: ReflectorProtocol.V2,
            CertEmail: null,
            // Section SimplexLogic
            SimplexCallsign: "F5ABC",
            Modules: "ModuleHelp,ModuleParrot",
            ShortIdentInterval: 60,
            LongIdentInterval: 60,
            ReportCtcss: "71.9",
            DefaultLang: "fr_FR",
            RgrSoundDelay: 0,
            // Références
            // Configuration Radio (valeurs par défaut pour tests)
            RxFrequency: 145.550m,
            TxFrequency: 145.550m,
            RxCtcss: 136.5m,
            TxCtcss: 136.5m);
    }

    private static SalonAggregate CreateValidAggregate()
    {
        var result = SalonAggregate.Create(
            Guid.NewGuid(),
            "Salon Test",
            isDefault: false,
            isTemporized: false,
            CreateValidConfiguration());

        return result.Match(
            Succ: aggregate => aggregate,
            Fail: errors => throw new InvalidOperationException($"Failed to create aggregate: {string.Join(", ", errors)}")
        );
    }

    #endregion
}
