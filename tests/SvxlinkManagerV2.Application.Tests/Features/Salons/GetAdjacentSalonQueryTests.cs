using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.GetAdjacentSalon;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests unitaires pour GetAdjacentSalonQuery et son handler (navigation DTMF 312 / 313)
/// </summary>
public class GetAdjacentSalonQueryTests
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;
    private readonly ILogger<GetAdjacentSalonQueryHandler> _logger;

    public GetAdjacentSalonQueryTests()
    {
        _repository = Substitute.For<ISalonRepository>();
        _tracker = Substitute.For<IActiveSessionTracker>();
        _logger = Substitute.For<ILogger<GetAdjacentSalonQueryHandler>>();
    }

    private GetAdjacentSalonQueryHandler CreateHandler() => new(_repository, _tracker, _logger);

    private async Task<SalonAggregate?> HandleAsync(SalonNavigationDirection direction) =>
        await CreateHandler().Handle(new GetAdjacentSalonQuery(direction), CancellationToken.None);

    [Fact]
    public async Task Handle_WithNoSalons_ShouldReturnNull()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await HandleAsync(SalonNavigationDirection.Next);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNoSalonHavingDtmfCode_ShouldReturnNull()
    {
        var salon = CreateAggregate("Sans code");
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([salon]);

        var result = await HandleAsync(SalonNavigationDirection.Next);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Next_ShouldReturnSalonWithNextDtmfCode()
    {
        var first = CreateAggregate("Premier", dtmfCode: 20);
        var second = CreateAggregate("Deuxième", dtmfCode: 30);
        var third = CreateAggregate("Troisième", dtmfCode: 40);

        // Ordre de retour volontairement mélangé : le handler doit trier par code DTMF.
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([third, first, second]);
        _tracker.ActiveSalonId.Returns(first.Id);

        var result = await HandleAsync(SalonNavigationDirection.Next);

        result!.Name.Should().Be("Deuxième");
    }

    [Fact]
    public async Task Handle_Previous_ShouldReturnSalonWithPreviousDtmfCode()
    {
        var first = CreateAggregate("Premier", dtmfCode: 20);
        var second = CreateAggregate("Deuxième", dtmfCode: 30);

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([first, second]);
        _tracker.ActiveSalonId.Returns(second.Id);

        var result = await HandleAsync(SalonNavigationDirection.Previous);

        result!.Name.Should().Be("Premier");
    }

    [Fact]
    public async Task Handle_Next_OnLastSalon_ShouldWrapToFirst()
    {
        var first = CreateAggregate("Premier", dtmfCode: 20);
        var last = CreateAggregate("Dernier", dtmfCode: 40);

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([first, last]);
        _tracker.ActiveSalonId.Returns(last.Id);

        var result = await HandleAsync(SalonNavigationDirection.Next);

        result!.Name.Should().Be("Premier");
    }

    [Fact]
    public async Task Handle_Previous_OnFirstSalon_ShouldWrapToLast()
    {
        var first = CreateAggregate("Premier", dtmfCode: 20);
        var last = CreateAggregate("Dernier", dtmfCode: 40);

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([first, last]);
        _tracker.ActiveSalonId.Returns(first.Id);

        var result = await HandleAsync(SalonNavigationDirection.Previous);

        result!.Name.Should().Be("Dernier");
    }

    [Fact]
    public async Task Handle_ShouldIgnoreDeletedSalons()
    {
        var active = CreateAggregate("Actif", dtmfCode: 20);
        var deleted = CreateAggregate("Supprimé", dtmfCode: 30);
        deleted.Delete();
        var next = CreateAggregate("Suivant", dtmfCode: 40);

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([active, deleted, next]);
        _tracker.ActiveSalonId.Returns(active.Id);

        var result = await HandleAsync(SalonNavigationDirection.Next);

        result!.Name.Should().Be("Suivant");
    }

    [Fact]
    public async Task Handle_ShouldIgnoreSalonsWithoutDtmfCode()
    {
        var active = CreateAggregate("Actif", dtmfCode: 20);
        var sansCode = CreateAggregate("Sans code");
        var next = CreateAggregate("Suivant", dtmfCode: 40);

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([active, sansCode, next]);
        _tracker.ActiveSalonId.Returns(active.Id);

        var result = await HandleAsync(SalonNavigationDirection.Next);

        result!.Name.Should().Be("Suivant");
    }

    [Fact]
    public async Task Handle_Next_WithNoActiveSalon_ShouldReturnFirst()
    {
        var first = CreateAggregate("Premier", dtmfCode: 20);
        var last = CreateAggregate("Dernier", dtmfCode: 40);

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([first, last]);
        _tracker.ActiveSalonId.Returns((Guid?)null);

        var result = await HandleAsync(SalonNavigationDirection.Next);

        result!.Name.Should().Be("Premier");
    }

    [Fact]
    public async Task Handle_Previous_WithNoActiveSalon_ShouldReturnLast()
    {
        var first = CreateAggregate("Premier", dtmfCode: 20);
        var last = CreateAggregate("Dernier", dtmfCode: 40);

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([first, last]);
        _tracker.ActiveSalonId.Returns((Guid?)null);

        var result = await HandleAsync(SalonNavigationDirection.Previous);

        result!.Name.Should().Be("Dernier");
    }

    [Fact]
    public async Task Handle_WithActiveSalonWithoutDtmfCode_ShouldEnterListFromTheEdge()
    {
        // Le salon actif n'est pas navigable : on entre dans la liste par une extrémité.
        var active = CreateAggregate("Actif sans code");
        var first = CreateAggregate("Premier", dtmfCode: 20);
        var last = CreateAggregate("Dernier", dtmfCode: 40);

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([active, first, last]);
        _tracker.ActiveSalonId.Returns(active.Id);

        var next = await HandleAsync(SalonNavigationDirection.Next);
        var previous = await HandleAsync(SalonNavigationDirection.Previous);

        next!.Name.Should().Be("Premier");
        previous!.Name.Should().Be("Dernier");
    }

    [Fact]
    public async Task Handle_WithSingleNavigableSalon_ShouldReturnItself()
    {
        var only = CreateAggregate("Unique", dtmfCode: 20);

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([only]);
        _tracker.ActiveSalonId.Returns(only.Id);

        var next = await HandleAsync(SalonNavigationDirection.Next);
        var previous = await HandleAsync(SalonNavigationDirection.Previous);

        next!.Id.Should().Be(only.Id);
        previous!.Id.Should().Be(only.Id);
    }

    private static SalonAggregate CreateAggregate(string name, int? dtmfCode = null)
    {
        var aggregate = SalonAggregate.Create(
            Guid.NewGuid(),
            name,
            isDefault: false,
            CreateValidConfiguration())
            .Match(
                Succ: a => a,
                Fail: errors => throw new InvalidOperationException($"Failed to create aggregate: {string.Join(", ", errors)}"));

        if (dtmfCode.HasValue)
            aggregate.UpdateDtmfCode(dtmfCode.Value);

        return aggregate;
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
