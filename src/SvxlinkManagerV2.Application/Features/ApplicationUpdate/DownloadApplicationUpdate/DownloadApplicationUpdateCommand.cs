using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.ApplicationUpdate.DownloadApplicationUpdate;

/// <summary>
/// Commande de téléchargement local du paquet de mise à jour.
/// </summary>
public record DownloadApplicationUpdateCommand(ApplicationUpdateChannel? Channel = null)
    : IRequest<Validation<Error, ApplicationUpdateWorkflowStatusDto>>;

/// <summary>
/// Handler pour DownloadApplicationUpdateCommand.
/// </summary>
public class DownloadApplicationUpdateCommandHandler
    : IRequestHandler<DownloadApplicationUpdateCommand, Validation<Error, ApplicationUpdateWorkflowStatusDto>>
{
    private readonly IApplicationUpdateWorkflowService _workflowService;

    public DownloadApplicationUpdateCommandHandler(IApplicationUpdateWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> Handle(
        DownloadApplicationUpdateCommand request,
        CancellationToken cancellationToken)
        => _workflowService.DownloadLatestAsync(request.Channel, cancellationToken);
}