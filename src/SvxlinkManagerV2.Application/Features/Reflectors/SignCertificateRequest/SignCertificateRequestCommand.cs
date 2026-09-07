using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Reflectors.SignCertificateRequest;

/// <summary>
/// Signe la demande de certificat en attente d'un nœud, sur le réflecteur local.
/// </summary>
/// <param name="Callsign">Indicatif du nœud demandeur, tel que listé dans les demandes.</param>
/// <remarks>
/// C'est une décision d'exploitation : signer autorise durablement ce nœud à se connecter au
/// réflecteur. Rien ne vérifie l'identité du demandeur — c'est précisément pourquoi la
/// signature est demandée à un humain plutôt qu'accordée automatiquement.
/// </remarks>
public record SignCertificateRequestCommand(string Callsign) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler de <see cref="SignCertificateRequestCommand"/>.
/// </summary>
/// <remarks>
/// Le succès rapporté n'est que celui de la <b>transmission</b> de la commande : le démon
/// signe de son côté, et la disparition de la demande de la liste en est la confirmation.
/// </remarks>
public class SignCertificateRequestCommandHandler
    : IRequestHandler<SignCertificateRequestCommand, Validation<Error, Unit>>
{
    private readonly IReflectorCommandWriter _commandWriter;
    private readonly ILogger<SignCertificateRequestCommandHandler> _logger;

    public SignCertificateRequestCommandHandler(
        IReflectorCommandWriter commandWriter,
        ILogger<SignCertificateRequestCommandHandler> logger)
    {
        _commandWriter = commandWriter;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        SignCertificateRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (!ReflectorCallsign.IsValid(command.Callsign))
            return Error.Validation("CERTIFICATE_CALLSIGN_INVALID", "Indicatif invalide").ToFailure<Unit>();

        var callsign = command.Callsign.Trim();
        _logger.LogWarning("Signature de la demande de certificat de {Callsign}", callsign);

        var result = await _commandWriter.SendCommandAsync($"CA SIGN {callsign}", cancellationToken);

        return result.Match(
            Succ: _ => unit.ToSuccess(),
            Fail: errors =>
            {
                _logger.LogError(
                    "Échec de la signature de {Callsign} : {Errors}",
                    callsign, string.Join(", ", errors.Select(e => e.Message)));

                return Error.Validation(
                    "CERTIFICATE_SIGN_FAILED",
                    "Impossible de transmettre la demande de signature au réflecteur")
                    .ToFailure<Unit>();
            });
    }
}
