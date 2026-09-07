using FluentAssertions;
using LanguageExt;
using LanguageExt.UnitTesting;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.SvxLink.ResetReflectorTrust;
using SvxlinkManagerV2.Application.Features.SvxLink.RestartSvxLink;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;
using LangExtError = LanguageExt.Common.Error;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Tests.Features.SvxLink;

/// <summary>
/// Tests de la réinitialisation de la confiance envers l'autorité du réflecteur.
/// </summary>
public class ResetReflectorTrustCommandTests
{
    private readonly IReflectorTrustService _trustService = Substitute.For<IReflectorTrustService>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<ResetReflectorTrustCommandHandler> _logger =
        Substitute.For<ILogger<ResetReflectorTrustCommandHandler>>();

    private Task<Validation<Error, Unit>> CallHandle()
    {
        var handler = new ResetReflectorTrustCommandHandler(_trustService, _mediator, _logger);
        return handler.Handle(new ResetReflectorTrustCommand(), CancellationToken.None);
    }

    private void GivenRestartSucceeds() =>
        _mediator.Send(Arg.Any<RestartSvxLinkCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<Error, Unit>>(unit.ToSuccess()));

    [Fact]
    public async Task Handle_ShouldForgetTheAuthorityThenRestartSvxLink()
    {
        // Le redémarrage est indispensable : SVXLink ne relit son bundle CA qu'à
        // l'ouverture d'une nouvelle session TLS.
        _trustService.ResetTrustAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, bool>>(true));
        GivenRestartSucceeds();

        var result = await CallHandle();

        result.ShouldBeSuccess();
        await _trustService.Received(1).ResetTrustAsync(Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(Arg.Any<RestartSvxLinkCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutAnyStoredBundle_ShouldStillRestart()
    {
        // Un bundle absent n'est pas un échec : le nœud est déjà prêt à en retélécharger un,
        // encore faut-il qu'il rouvre une session.
        _trustService.ResetTrustAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, bool>>(false));
        GivenRestartSucceeds();

        var result = await CallHandle();

        result.ShouldBeSuccess();
        await _mediator.Received(1).Send(Arg.Any<RestartSvxLinkCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTheBundleCannotBeRemoved_ShouldFailWithoutRestarting()
    {
        // Redémarrer sans avoir rien changé remettrait le nœud dans le même échec, en
        // laissant croire à une réparation.
        _trustService.ResetTrustAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<LangExtError, bool>.Fail(Seq1(LangExtError.New("accès refusé")))));

        var result = await CallHandle();

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("REFLECTOR_TRUST_RESET_FAILED"));
        await _mediator.DidNotReceive().Send(Arg.Any<RestartSvxLinkCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTheRestartFails_ShouldPropagateTheFailure()
    {
        _trustService.ResetTrustAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, bool>>(true));
        _mediator.Send(Arg.Any<RestartSvxLinkCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Error.Validation("SVXLINK_RESTART_ERROR", "Impossible de redémarrer").ToFailure<Unit>()));

        var result = await CallHandle();

        result.ShouldBeFail(errors => errors.Head.Code.Should().Be("SVXLINK_RESTART_ERROR"));
    }
}
