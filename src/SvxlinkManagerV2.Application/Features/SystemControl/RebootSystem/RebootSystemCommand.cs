using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.SystemControl.RebootSystem;

/// <summary>
/// Commande de redémarrage de la machine hôte.
/// </summary>
public record RebootSystemCommand() : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour RebootSystemCommand.
/// </summary>
public class RebootSystemCommandHandler
    : SystemPowerCommandHandlerBase, IRequestHandler<RebootSystemCommand, Validation<Error, Unit>>
{
    private readonly ISystemControlService _systemControlService;

    public RebootSystemCommandHandler(
        ISystemControlService systemControlService,
        ISvxLinkDaemonService svxLinkDaemonService,
        IReflectorDaemonService reflectorDaemonService,
        ILogger<RebootSystemCommandHandler> logger)
        : base(systemControlService, svxLinkDaemonService, reflectorDaemonService, logger)
    {
        _systemControlService = systemControlService;
    }

    public Task<Validation<Error, Unit>> Handle(
        RebootSystemCommand command,
        CancellationToken cancellationToken)
        => ExecuteAsync("Redémarrage", _systemControlService.RebootAsync, cancellationToken);
}
