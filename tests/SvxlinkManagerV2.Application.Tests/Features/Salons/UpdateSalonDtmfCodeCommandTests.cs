using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.UpdateDtmfCode;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour UpdateSalonDtmfCodeCommand et son handler
/// </summary>
public class UpdateSalonDtmfCodeCommandTests
{
    private readonly ISalonRepository _repository;
    private readonly ILogger<UpdateSalonDtmfCodeCommandHandler> _logger;

    public UpdateSalonDtmfCodeCommandTests()
    {
        _repository = Substitute.For<ISalonRepository>();
        _logger = Substitute.For<ILogger<UpdateSalonDtmfCodeCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WithValidDtmfCode_ShouldSucceed()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var aggregate = CreateValidAggregate(salonId);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());
        _repository.GetByDtmfCodeAsync(96, Arg.Any<CancellationToken>())
            .Returns((SalonAggregate?)null);
        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        var command = new UpdateSalonDtmfCodeCommand(salonId, 96);

        // Act
        var result = await new UpdateSalonDtmfCodeCommandHandler(_repository, _logger)
            .Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.DtmfCode.Should().Be(96);
        });

        await _repository.Received(1).SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNullDtmfCode_ShouldClearCode()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        var aggregate = CreateValidAggregate(salonId);
        aggregate.UpdateDtmfCode(42); // Set a code first

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());
        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        var command = new UpdateSalonDtmfCodeCommand(salonId, null);

        // Act
        var result = await new UpdateSalonDtmfCodeCommandHandler(_repository, _logger)
            .Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess(_ =>
        {
            aggregate.DtmfCode.Should().BeNull();
        });
    }

    [Fact]
    public async Task Handle_WithDuplicateDtmfCode_ShouldFail()
    {
        // Arrange
        var salonId1 = Guid.NewGuid();
        var salonId2 = Guid.NewGuid();
        var aggregate1 = CreateValidAggregate(salonId1);
        var aggregate2 = CreateValidAggregate(salonId2, "Salon Existant");
        aggregate2.UpdateDtmfCode(96);

        _repository.GetByIdAsync(salonId1, Arg.Any<CancellationToken>())
            .Returns(aggregate1.ToSuccess());
        _repository.GetByDtmfCodeAsync(96, Arg.Any<CancellationToken>())
            .Returns(aggregate2);

        var command = new UpdateSalonDtmfCodeCommand(salonId1, 96);

        // Act
        var result = await new UpdateSalonDtmfCodeCommandHandler(_repository, _logger)
            .Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code == "DTMF_CODE_ALREADY_USED");
        });

        await _repository.DidNotReceive().SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSameSalonSameCode_ShouldSucceed()
    {
        // Arrange - Le même salon peut garder son propre code
        var salonId = Guid.NewGuid();
        var aggregate = CreateValidAggregate(salonId);
        aggregate.UpdateDtmfCode(96);

        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(aggregate.ToSuccess());
        _repository.GetByDtmfCodeAsync(96, Arg.Any<CancellationToken>())
            .Returns(aggregate);
        _repository.SaveAsync(Arg.Any<SalonAggregate>(), Arg.Any<CancellationToken>())
            .Returns(unit.ToSuccess());

        var command = new UpdateSalonDtmfCodeCommand(salonId, 96);

        // Act
        var result = await new UpdateSalonDtmfCodeCommandHandler(_repository, _logger)
            .Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task Handle_WithNonExistentSalon_ShouldFail()
    {
        // Arrange
        var salonId = Guid.NewGuid();
        _repository.GetByIdAsync(salonId, Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Salon", salonId).ToFailure<SalonAggregate>());

        var command = new UpdateSalonDtmfCodeCommand(salonId, 96);

        // Act
        var result = await new UpdateSalonDtmfCodeCommandHandler(_repository, _logger)
            .Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeFail(errors =>
        {
            errors.Should().Contain(e => e.Code.Contains("NOT_FOUND"));
        });
    }

    private static SalonAggregate CreateValidAggregate(Guid? id = null, string name = "Salon Test")
    {
        var result = SalonAggregate.Create(
            id ?? Guid.NewGuid(),
            name,
            isDefault: false,
            isTemporized: false,
            CreateValidConfiguration());

        return result.Match(
            Succ: aggregate => aggregate,
            Fail: errors => throw new InvalidOperationException($"Failed to create aggregate: {string.Join(", ", errors)}")
        );
    }

    private static SvxLinkConfiguration CreateValidConfiguration() => new(
        Guid.NewGuid(),
        Logics: "SimplexLogic,ReflectorLogic",
        CfgDir: "svxlink.d",
        CardSampleRate: 16000,
        CardChannels: 1,
        Host: "ref.f5kri.fr",
        Port: 5300,
        Callsign: "F5ABC-L",
        AuthKey: "test-auth-key-123",
        JitterBufferDelay: 0,
        ReflectorProtocol: ReflectorProtocol.V2,
        CertEmail: null,
        SimplexCallsign: "F5ABC",
        Modules: "ModuleHelp,ModuleParrot",
        ShortIdentInterval: 60,
        LongIdentInterval: 60,
        ReportCtcss: "71.9",
        DefaultLang: "fr_FR",
        RgrSoundDelay: 0,
        RxFrequency: 145.550m,
        TxFrequency: 145.550m,
        RxCtcss: 136.5m,
        TxCtcss: 136.5m);
}
