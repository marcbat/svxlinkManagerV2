using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.ApplicationUpdate.GetApplicationUpdateStatus;

/// <summary>
/// Query de consultation du statut de mise à jour applicative.
/// </summary>
public record GetApplicationUpdateStatusQuery(ApplicationUpdateChannel? Channel = null)
    : IRequest<Validation<Error, ApplicationUpdateStatusDto>>;

/// <summary>
/// Handler pour la query GetApplicationUpdateStatusQuery.
/// </summary>
public class GetApplicationUpdateStatusQueryHandler
    : IRequestHandler<GetApplicationUpdateStatusQuery, Validation<Error, ApplicationUpdateStatusDto>>
{
    private readonly IApplicationUpdateService _applicationUpdateService;

    public GetApplicationUpdateStatusQueryHandler(IApplicationUpdateService applicationUpdateService)
    {
        _applicationUpdateService = applicationUpdateService;
    }

    public Task<Validation<Error, ApplicationUpdateStatusDto>> Handle(
        GetApplicationUpdateStatusQuery request,
        CancellationToken cancellationToken)
        => _applicationUpdateService.GetStatusAsync(request.Channel, refreshIndex: true, cancellationToken);
}