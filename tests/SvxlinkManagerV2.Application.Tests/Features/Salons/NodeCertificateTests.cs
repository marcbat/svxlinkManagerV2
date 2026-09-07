using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Salons.GetNodeCertificate;
using SvxlinkManagerV2.Application.Features.Salons.RegenerateCertificateRequest;
using SvxlinkManagerV2.Application.Features.SvxLink.RestartSvxLink;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;
using LangExtError = LanguageExt.Common.Error;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Tests.Features.Salons;

/// <summary>
/// Tests de la lecture et de la régénération du certificat du nœud.
/// </summary>
public class NodeCertificateTests
{
    private const string Callsign = "HB9GXP-H";

    private readonly ISalonRepository _repository = Substitute.For<ISalonRepository>();
    private readonly IActiveSessionTracker _tracker = Substitute.For<IActiveSessionTracker>();
    private readonly INodeCertificateReader _reader = Substitute.For<INodeCertificateReader>();
    private readonly INodeCertificateRequestResetter _resetter =
        Substitute.For<INodeCertificateRequestResetter>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private void GivenActiveSalon(SalonAggregate salon)
    {
        _tracker.ActiveSalonId.Returns(salon.Id);
        _repository.GetByIdAsync(salon.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<Error, SalonAggregate>>(salon.ToSuccess()));
    }

    private Task<NodeCertificateState> ReadCertificate() =>
        new GetNodeCertificateQueryHandler(_repository, _tracker, _reader)
            .Handle(new GetNodeCertificateQuery(), CancellationToken.None);

    private Task<Validation<Error, Unit>> Regenerate() =>
        new RegenerateCertificateRequestCommandHandler(
                _repository, _tracker, _resetter, _mediator,
                Substitute.For<ILogger<RegenerateCertificateRequestCommandHandler>>())
            .Handle(new RegenerateCertificateRequestCommand(), CancellationToken.None);

    #region Lecture

    [Fact]
    public async Task Read_WithAV3Salon_ShouldReadThePkiForItsCallsign()
    {
        GivenActiveSalon(CreateSalon(ReflectorProtocol.V3));
        _reader.Read(Callsign).Returns(new NodeCertificateState(
            NodeCertificateStatus.PendingSignature, Callsign));

        var state = await ReadCertificate();

        state.Status.Should().Be(NodeCertificateStatus.PendingSignature);
        _reader.Received(1).Read(Callsign);
    }

    [Fact]
    public async Task Read_WithAV2Salon_ShouldReturnNotApplicableWithoutTouchingThePki()
    {
        // SVXLink 19.09.2 s'authentifie par AUTH_KEY : il n'a pas de certificat.
        GivenActiveSalon(CreateSalon(ReflectorProtocol.V2));

        var state = await ReadCertificate();

        state.Status.Should().Be(NodeCertificateStatus.NotApplicable);
        _reader.DidNotReceive().Read(Arg.Any<string>());
    }

    [Fact]
    public async Task Read_WithAParrotSalon_ShouldReturnNotApplicable()
    {
        GivenActiveSalon(CreateSalon(ReflectorProtocol.V3, SalonType.Parrot));

        (await ReadCertificate()).Status.Should().Be(NodeCertificateStatus.NotApplicable);
    }

    [Fact]
    public async Task Read_WithoutAnActiveSalon_ShouldReturnNotApplicable()
    {
        _tracker.ActiveSalonId.Returns((Guid?)null);

        (await ReadCertificate()).Status.Should().Be(NodeCertificateStatus.NotApplicable);
    }

    #endregion

    #region Régénération

    [Fact]
    public async Task Regenerate_ShouldResetThePkiThenRestartSvxLink()
    {
        // Sans redémarrage, le processus en cours garderait en mémoire le certificat effacé
        // et ne déposerait aucune nouvelle demande.
        GivenActiveSalon(CreateSalon(ReflectorProtocol.V3));
        _resetter.ResetAsync(Callsign, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, bool>>(true));
        _mediator.Send(Arg.Any<RestartSvxLinkCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<Error, Unit>>(unit.ToSuccess()));

        var result = await Regenerate();

        result.ShouldBeSuccess();
        await _resetter.Received(1).ResetAsync(Callsign, Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(Arg.Any<RestartSvxLinkCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Regenerate_WithAV2Salon_ShouldRefuse()
    {
        // Effacer des fichiers de PKI n'aiderait pas un salon qui n'en a pas.
        GivenActiveSalon(CreateSalon(ReflectorProtocol.V2));

        var result = await Regenerate();

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("CERTIFICATE_NOT_SUPPORTED"));
        await _resetter.DidNotReceive().ResetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Regenerate_WithoutAnActiveSalon_ShouldRefuse()
    {
        _tracker.ActiveSalonId.Returns((Guid?)null);

        var result = await Regenerate();

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("CERTIFICATE_NO_ACTIVE_SALON"));
    }

    [Fact]
    public async Task Regenerate_WhenThePkiCannotBeReset_ShouldFailWithoutRestarting()
    {
        // Redémarrer sans avoir rien effacé remettrait le nœud dans le même état, en laissant
        // croire à une réparation.
        GivenActiveSalon(CreateSalon(ReflectorProtocol.V3));
        _resetter.ResetAsync(Callsign, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<LangExtError, bool>.Fail(Seq1(LangExtError.New("accès refusé")))));

        var result = await Regenerate();

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("CERTIFICATE_RESET_FAILED"));
        await _mediator.DidNotReceive().Send(Arg.Any<RestartSvxLinkCommand>(), Arg.Any<CancellationToken>());
    }

    #endregion

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
            Callsign,
            protocol == ReflectorProtocol.V2 ? "test-auth-key" : null,
            0,
            protocol,
            null,
            "HB9GXP",
            "ModuleHelp",
            60,
            60,
            null,
            "fr_FR",
            0,
            145.550m,
            145.550m,
            136.5m,
            136.5m);

        return SalonAggregate.Create(Guid.NewGuid(), "Salon Test", false, config, salonType).Match(
            Succ: aggregate => aggregate,
            Fail: errors => throw new InvalidOperationException(string.Join(", ", errors)));
    }
}
