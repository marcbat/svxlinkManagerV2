using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Reflectors.BlockNode;

/// <summary>
/// Empêche temporairement un nœud d'émettre sur le réflecteur local, sans le déconnecter.
/// </summary>
/// <param name="Callsign">Indicatif du nœud à museler.</param>
/// <param name="Seconds">Durée du blocage, en secondes.</param>
/// <remarks>
/// C'est l'outil du sysop face à un nœud resté en émission — micro coincé, VOX déréglé —
/// qui monopolise le talkgroup. Le nœud conserve l'écoute : le but est de rendre le
/// réflecteur utilisable, pas de punir.
///
/// Le blocage n'est pas persisté par le démon : il tombe à son redémarrage.
/// </remarks>
public record BlockNodeCommand(string Callsign, int Seconds) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler de <see cref="BlockNodeCommand"/>.
/// </summary>
public class BlockNodeCommandHandler : IRequestHandler<BlockNodeCommand, Validation<Error, Unit>>
{
    /// <summary>
    /// Plafond volontairement fini : un blocage se lève tout seul, et le démon oublie de
    /// toute façon la consigne à son redémarrage. Une valeur démesurée donnerait l'illusion
    /// d'une exclusion définitive.
    /// </summary>
    internal const int MaximumSeconds = 86400;

    private readonly IReflectorCommandWriter _commandWriter;
    private readonly ILogger<BlockNodeCommandHandler> _logger;

    public BlockNodeCommandHandler(
        IReflectorCommandWriter commandWriter,
        ILogger<BlockNodeCommandHandler> logger)
    {
        _commandWriter = commandWriter;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        BlockNodeCommand command,
        CancellationToken cancellationToken)
    {
        if (!ReflectorCallsign.IsValid(command.Callsign))
            return Error.Validation("NODE_CALLSIGN_INVALID", "Indicatif invalide").ToFailure<Unit>();

        if (command.Seconds is <= 0 or > MaximumSeconds)
            return Error.Validation(
                "NODE_BLOCK_DURATION_INVALID",
                $"La durée de blocage doit être comprise entre 1 et {MaximumSeconds} secondes")
                .ToFailure<Unit>();

        var callsign = command.Callsign.Trim();
        _logger.LogWarning("Blocage du nœud {Callsign} pendant {Seconds} s", callsign, command.Seconds);

        var result = await _commandWriter.SendCommandAsync(
            $"NODE BLOCK {callsign} {command.Seconds}", cancellationToken);

        return result.Match(
            Succ: _ => unit.ToSuccess(),
            Fail: errors =>
            {
                _logger.LogError(
                    "Échec du blocage de {Callsign} : {Errors}",
                    callsign, string.Join(", ", errors.Select(e => e.Message)));

                return Error.Validation(
                    "NODE_BLOCK_FAILED",
                    "Impossible de transmettre la demande de blocage au réflecteur")
                    .ToFailure<Unit>();
            });
    }
}
