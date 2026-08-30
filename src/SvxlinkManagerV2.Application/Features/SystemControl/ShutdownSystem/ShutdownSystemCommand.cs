using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.SystemControl.ShutdownSystem;

/// <summary>
/// Commande d'arrêt de la machine hôte.
/// </summary>
public record ShutdownSystemCommand() : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour ShutdownSystemCommand.
/// </summary>
public class ShutdownSystemCommandHandler
    : SystemPowerCommandHandlerBase, IRequestHandler<ShutdownSystemCommand, Validation<Error, Unit>>
{
    private readonly ISystemControlService _systemControlService;

    public ShutdownSystemCommandHandler(
        ISystemControlService systemControlService,
        ISvxLinkDaemonService svxLinkDaemonService,
        IReflectorDaemonService reflectorDaemonService,
        ILogger<ShutdownSystemCommandHandler> logger)
        : base(systemControlService, svxLinkDaemonService, reflectorDaemonService, logger)
    {
        _systemControlService = systemControlService;
    }

    public Task<Validation<Error, Unit>> Handle(
        ShutdownSystemCommand command,
        CancellationToken cancellationToken)
        => ExecuteAsync("Arrêt", _systemControlService.ShutdownAsync, cancellationToken);
}
