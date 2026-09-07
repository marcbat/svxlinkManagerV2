using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.SvxLink.RestartSvxLink;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.SvxLink.ResetReflectorTrust;

/// <summary>
/// Fait oublier au nœud l'autorité de certification qu'il connaît, puis redémarre SVXLink
/// pour qu'il retélécharge celle du réflecteur.
/// </summary>
/// <remarks>
/// Remède du cas <see cref="Models.ReflectorLinkFailureReason.ServerCertificateUntrusted"/> :
/// le réflecteur a régénéré sa PKI et le nœud refuse la nouvelle chaîne. Sans cette
/// commande, il faut un accès shell au nœud pour effacer le fichier — précisément ce que
/// l'application est censée éviter sur une machine sans clavier.
///
/// Le redémarrage est nécessaire : SVXLink ne relit son bundle CA qu'à l'ouverture d'une
/// nouvelle session TLS, et le processus en cours garderait sa vue périmée.
/// </remarks>
public record ResetReflectorTrustCommand() : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler de <see cref="ResetReflectorTrustCommand"/>.
/// </summary>
public class ResetReflectorTrustCommandHandler
    : IRequestHandler<ResetReflectorTrustCommand, Validation<Error, Unit>>
{
    private readonly IReflectorTrustService _trustService;
    private readonly IMediator _mediator;
    private readonly ILogger<ResetReflectorTrustCommandHandler> _logger;

    public ResetReflectorTrustCommandHandler(
        IReflectorTrustService trustService,
        IMediator mediator,
        ILogger<ResetReflectorTrustCommandHandler> logger)
    {
        _trustService = trustService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        ResetReflectorTrustCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Réinitialisation de la confiance envers le réflecteur");

        var resetResult = await _trustService.ResetTrustAsync(cancellationToken);
        if (resetResult.IsFail)
            return Error.Validation(
                "REFLECTOR_TRUST_RESET_FAILED",
                "Impossible de supprimer l'autorité de certification mémorisée")
                .ToFailure<Unit>();

        // Un bundle absent n'est pas un échec : le nœud est déjà prêt à retélécharger.
        // Le redémarrage reste utile, c'est lui qui rouvre une session TLS.
        return await _mediator.Send(new RestartSvxLinkCommand(), cancellationToken);
    }
}
