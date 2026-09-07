using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.SvxLink.RestartSvxLink;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Salons.RegenerateCertificateRequest;

/// <summary>
/// Efface la demande et le certificat du nœud, puis redémarre SVXLink pour qu'il dépose une
/// nouvelle demande de signature.
/// </summary>
/// <remarks>
/// <para>
/// C'est une opération à conséquence : le nœud perd son certificat et <b>ne pourra plus se
/// connecter tant que le sysop du réflecteur n'aura pas signé la nouvelle demande</b>. Sur un
/// réflecteur local c'est immédiat, depuis la page Certificats ; sur un réflecteur public,
/// l'attente dépend d'un tiers.
/// </para>
/// <para>
/// La clé privée est conservée : elle est l'identité du nœud, et rien n'oblige à la
/// renouveler pour refaire signer une demande.
/// </para>
/// </remarks>
public record RegenerateCertificateRequestCommand() : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler de <see cref="RegenerateCertificateRequestCommand"/>.
/// </summary>
public class RegenerateCertificateRequestCommandHandler
    : IRequestHandler<RegenerateCertificateRequestCommand, Validation<Error, Unit>>
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;
    private readonly INodeCertificateRequestResetter _resetter;
    private readonly IMediator _mediator;
    private readonly ILogger<RegenerateCertificateRequestCommandHandler> _logger;

    public RegenerateCertificateRequestCommandHandler(
        ISalonRepository repository,
        IActiveSessionTracker tracker,
        INodeCertificateRequestResetter resetter,
        IMediator mediator,
        ILogger<RegenerateCertificateRequestCommandHandler> logger)
    {
        _repository = repository;
        _tracker = tracker;
        _resetter = resetter;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        RegenerateCertificateRequestCommand command,
        CancellationToken cancellationToken)
    {
        var activeSalonId = _tracker.ActiveSalonId;
        if (!activeSalonId.HasValue)
            return Error.Validation("CERTIFICATE_NO_ACTIVE_SALON", "Aucun salon actif").ToFailure<Unit>();

        var aggregateResult = await _repository.GetByIdAsync(activeSalonId.Value, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: Validation<Error, Unit>.Fail);

        var salon = aggregateResult.Match(
            Succ: aggregate => aggregate,
            Fail: _ => throw new InvalidOperationException());

        // Un salon V2 n'a pas de certificat : effacer des fichiers au hasard ne l'aiderait pas.
        if (salon.SalonType == SalonType.Parrot ||
            salon.Configuration.ReflectorProtocol != ReflectorProtocol.V3)
            return Error.Validation(
                "CERTIFICATE_NOT_SUPPORTED",
                "Le salon actif n'utilise pas le protocole V3 : il n'a pas de certificat")
                .ToFailure<Unit>();

        _logger.LogWarning(
            "Régénération de la demande de certificat pour {Callsign}", salon.Configuration.Callsign);

        var resetResult = await _resetter.ResetAsync(salon.Configuration.Callsign, cancellationToken);
        if (resetResult.IsFail)
            return Error.Validation(
                "CERTIFICATE_RESET_FAILED",
                "Impossible de supprimer la demande et le certificat du nœud")
                .ToFailure<Unit>();

        // SVXLink ne regénère sa demande qu'au démarrage : sans redémarrage, le processus en
        // cours continuerait d'utiliser le certificat qu'il a déjà en mémoire.
        return await _mediator.Send(new RestartSvxLinkCommand(), cancellationToken);
    }
}
