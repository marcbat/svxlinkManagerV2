using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.SelectTalkGroup;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;
using LangExtError = LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests de la sélection de talkgroup à chaud.
/// La commande n'écrit rien en base et ne redémarre rien : elle compose une séquence DTMF
/// dans le PTY, exactement comme le ferait un opérateur depuis sa radio.
/// </summary>
public class SelectTalkGroupCommandTests
{
    private readonly ISalonRepository _repository = Substitute.For<ISalonRepository>();
    private readonly IActiveSessionTracker _sessionTracker = Substitute.For<IActiveSessionTracker>();
    private readonly IDtmfPtyWriter _dtmfPtyWriter = Substitute.For<IDtmfPtyWriter>();
    private readonly ILogger<SelectTalkGroupCommandHandler> _logger =
        Substitute.For<ILogger<SelectTalkGroupCommandHandler>>();

    private Task<Validation<Error, Unit>> CallHandle(int talkGroup)
    {
        var handler = new SelectTalkGroupCommandHandler(
            _repository, _sessionTracker, _dtmfPtyWriter, _logger);
        return handler.Handle(new SelectTalkGroupCommand(talkGroup), CancellationToken.None);
    }

    private void GivenActiveSalon(SalonAggregate salon)
    {
        _sessionTracker.ActiveSalonId.Returns(salon.Id);
        _repository.GetByIdAsync(salon.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<Error, SalonAggregate>>(salon.ToSuccess()));
    }

    private void GivenPtyAccepts() =>
        _dtmfPtyWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(unit));

    [Fact]
    public async Task Handle_WithV3Salon_ShouldComposeThePrefixedSelectSequence()
    {
        GivenActiveSalon(CreateSalon(ReflectorProtocol.V3));
        GivenPtyAccepts();

        var result = await CallHandle(240);

        result.ShouldBeSuccess();
        await _dtmfPtyWriter.Received(1).SendCommandAsync("351240", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithTalkGroupZero_ShouldBeAccepted()
    {
        // 0 n'est pas une absence de valeur : SVXLink l'interprète comme « aucun talkgroup ».
        GivenActiveSalon(CreateSalon(ReflectorProtocol.V3));
        GivenPtyAccepts();

        var result = await CallHandle(0);

        result.ShouldBeSuccess();
        await _dtmfPtyWriter.Received(1).SendCommandAsync("3510", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNegativeTalkGroup_ShouldFail()
    {
        var result = await CallHandle(-1);

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("TALKGROUP_INVALID"));
        await _dtmfPtyWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutActiveSalon_ShouldFail()
    {
        _sessionTracker.ActiveSalonId.Returns((Guid?)null);

        var result = await CallHandle(240);

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("TALKGROUP_NO_ACTIVE_SALON"));
        await _dtmfPtyWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithV2Salon_ShouldFail()
    {
        // Aucun préfixe de commande n'est déclaré pour un salon V2 : la séquence se perdrait
        // sans le moindre message d'erreur côté SVXLink.
        GivenActiveSalon(CreateSalon(ReflectorProtocol.V2));

        var result = await CallHandle(240);

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("TALKGROUP_NOT_SUPPORTED"));
        await _dtmfPtyWriter.DidNotReceive().SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithParrotSalon_ShouldFail()
    {
        GivenActiveSalon(CreateSalon(ReflectorProtocol.V3, SalonType.Parrot));

        var result = await CallHandle(240);

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("TALKGROUP_NOT_SUPPORTED"));
    }

    [Fact]
    public async Task Handle_WhenThePtyIsUnavailable_ShouldFail()
    {
        GivenActiveSalon(CreateSalon(ReflectorProtocol.V3));
        _dtmfPtyWriter.SendCommandAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, Unit>>(
                Validation<LangExtError, Unit>.Fail(Seq1(LangExtError.New("PTY introuvable")))));

        var result = await CallHandle(240);

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("TALKGROUP_COMMAND_FAILED"));
    }

    private static SalonAggregate CreateSalon(
        ReflectorProtocol protocol,
        SalonType salonType = SalonType.Reflector)
    {
        var config = new SvxLinkConfiguration(
            Guid.NewGuid(),
            "SimplexLogic,ReflectorLogic",
            "svxlink.d",
            16000,
            1,
            "ref.f5kri.fr",
            5300,
            "F5ABC-L",
            protocol == ReflectorProtocol.V2 ? "test-auth-key" : null,
            0,
            protocol,
            null,
            "F5ABC",
            "ModuleHelp",
            60,
            60,
            null,
            "fr_FR",
            0,
            145.550m,
            145.550m,
            136.5m,
            136.5m,
            DefaultTg: 2403,
            MonitorTgs: "240,2404");

        return SalonAggregate.Create(Guid.NewGuid(), "Salon Test", false, config, salonType).Match(
            Succ: a => a,
            Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));
    }
}
