using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.GetSalonByDtmfCode;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour GetSalonByDtmfCodeQuery et son handler
/// </summary>
public class GetSalonByDtmfCodeQueryTests
{
    private readonly ISalonRepository _repository;
    private readonly ILogger<GetSalonByDtmfCodeQueryHandler> _logger;

    public GetSalonByDtmfCodeQueryTests()
    {
        _repository = Substitute.For<ISalonRepository>();
        _logger = Substitute.For<ILogger<GetSalonByDtmfCodeQueryHandler>>();
    }

    [Fact]
    public async Task Handle_WithExistingDtmfCode_ShouldReturnSalon()
    {
        // Arrange
        var aggregate = CreateValidAggregate();
        aggregate.UpdateDtmfCode(96);

        _repository.GetByDtmfCodeAsync(96, Arg.Any<CancellationToken>())
            .Returns(aggregate);

        var query = new GetSalonByDtmfCodeQuery(96);

        // Act
        var result = await new GetSalonByDtmfCodeQueryHandler(_repository, _logger)
            .Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(aggregate.Id);
        result.DtmfCode.Should().Be(96);
    }

    [Fact]
    public async Task Handle_WithNonExistingDtmfCode_ShouldReturnNull()
    {
        // Arrange
        _repository.GetByDtmfCodeAsync(42, Arg.Any<CancellationToken>())
            .Returns((SalonAggregate?)null);

        var query = new GetSalonByDtmfCodeQuery(42);

        // Act
        var result = await new GetSalonByDtmfCodeQueryHandler(_repository, _logger)
            .Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNoSalonsHavingDtmfCode_ShouldReturnNull()
    {
        // Arrange
        _repository.GetByDtmfCodeAsync(96, Arg.Any<CancellationToken>())
            .Returns((SalonAggregate?)null);

        var query = new GetSalonByDtmfCodeQuery(96);

        // Act
        var result = await new GetSalonByDtmfCodeQueryHandler(_repository, _logger)
            .Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithMultipleSalons_ShouldReturnCorrectOne()
    {
        // Arrange
        var aggregate2 = CreateValidAggregate(name: "Salon 2");
        aggregate2.UpdateDtmfCode(97);

        _repository.GetByDtmfCodeAsync(97, Arg.Any<CancellationToken>())
            .Returns(aggregate2);

        var query = new GetSalonByDtmfCodeQuery(97);

        // Act
        var result = await new GetSalonByDtmfCodeQueryHandler(_repository, _logger)
            .Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Salon 2");
    }

    private static SalonAggregate CreateValidAggregate(string name = "Salon Test")
    {
        var result = SalonAggregate.Create(
            Guid.NewGuid(),
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
        AudioCodec: "OPUS",
        JitterBufferDelay: 0,
        SimplexCallsign: "F5ABC",
        Modules: "ModuleHelp,ModuleParrot",
        ShortIdentInterval: 60,
        LongIdentInterval: 60,
        ReportCtcss: "71.9",
        EventHandler: "/usr/share/svxlink/events.tcl",
        DefaultLang: "fr_FR",
        RgrSoundDelay: 0,
        RxFrequency: 145.550m,
        TxFrequency: 145.550m,
        RxCtcss: 136.5m,
        TxCtcss: 136.5m);
}
