using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.ApplicationUpdate.RequestApplicationUpdateInstallation;

/// <summary>
/// Commande de demande d'installation du paquet déjà téléchargé.
/// </summary>
public record RequestApplicationUpdateInstallationCommand()
    : IRequest<Validation<Error, ApplicationUpdateWorkflowStatusDto>>;

/// <summary>
/// Handler pour RequestApplicationUpdateInstallationCommand.
/// </summary>
public class RequestApplicationUpdateInstallationCommandHandler
    : IRequestHandler<RequestApplicationUpdateInstallationCommand, Validation<Error, ApplicationUpdateWorkflowStatusDto>>
{
    private readonly IApplicationUpdateWorkflowService _workflowService;

    public RequestApplicationUpdateInstallationCommandHandler(IApplicationUpdateWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> Handle(
        RequestApplicationUpdateInstallationCommand request,
        CancellationToken cancellationToken)
        => _workflowService.RequestInstallAsync(cancellationToken);
}