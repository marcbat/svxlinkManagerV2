using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.ApplicationUpdate.GetApplicationUpdateWorkflowStatus;

/// <summary>
/// Query de consultation du workflow complet de mise à jour applicative.
/// </summary>
/// <param name="Channel">Canal à consulter, ou null pour celui déjà configuré.</param>
/// <param name="RefreshIndex">
/// Interroge le dépôt distant avant de répondre. À laisser à false pour un simple
/// affichage : la consultation distante prend une dizaine de secondes sur un Orange Pi.
/// </param>
public record GetApplicationUpdateWorkflowStatusQuery(
    ApplicationUpdateChannel? Channel = null,
    bool RefreshIndex = true)
    : IRequest<Validation<Error, ApplicationUpdateWorkflowStatusDto>>;

/// <summary>
/// Handler pour GetApplicationUpdateWorkflowStatusQuery.
/// </summary>
public class GetApplicationUpdateWorkflowStatusQueryHandler
    : IRequestHandler<GetApplicationUpdateWorkflowStatusQuery, Validation<Error, ApplicationUpdateWorkflowStatusDto>>
{
    private readonly IApplicationUpdateWorkflowService _workflowService;

    public GetApplicationUpdateWorkflowStatusQueryHandler(IApplicationUpdateWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> Handle(
        GetApplicationUpdateWorkflowStatusQuery request,
        CancellationToken cancellationToken)
        => _workflowService.GetStatusAsync(request.Channel, request.RefreshIndex, cancellationToken);
}