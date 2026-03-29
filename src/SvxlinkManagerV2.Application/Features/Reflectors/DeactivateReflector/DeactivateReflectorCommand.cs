using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Reflectors.DeactivateReflector;

/// <summary>
/// Commande pour désactiver le Reflector (arrête le daemon svxreflector).
/// </summary>
/// <param name="Id">Identifiant unique du reflector à désactiver</param>
public record DeactivateReflectorCommand(Guid Id) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande DeactivateReflectorCommand.
/// </summary>
public class DeactivateReflectorCommandHandler : IRequestHandler<DeactivateReflectorCommand, Validation<Error, Unit>>
{
    private readonly IActiveSessionTracker _tracker;
    private readonly IReflectorDaemonService _daemonService;
    private readonly ILogger<DeactivateReflectorCommandHandler> _logger;

    public DeactivateReflectorCommandHandler(
        IActiveSessionTracker tracker,
        IReflectorDaemonService daemonService,
        ILogger<DeactivateReflectorCommandHandler> logger)
    {
        _tracker = tracker;
        _daemonService = daemonService;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        DeactivateReflectorCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Désactivation du Reflector {ReflectorId}", command.Id);

        if (!_tracker.IsReflectorActive(command.Id))
            return Error.Validation("REFLECTOR_NOT_ACTIVE", "Ce reflector n'est pas actuellement actif").ToFailure<Unit>();

        var result = await _daemonService.StopAsync(cancellationToken);
        if (result.IsFail)
            return Error.Validation("REFLECTOR_STOP_ERROR", "Impossible d'arrêter le daemon svxreflector").ToFailure<Unit>();

        _tracker.SetActiveReflector(null);

        _logger.LogInformation("Reflector {ReflectorId} désactivé avec succès", command.Id);
        return unit.ToSuccess();
    }
}
