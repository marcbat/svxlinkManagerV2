using MediatR;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Application.Features.SystemControl.GetSystemControlAvailability;

/// <summary>
/// Query pour connaître la disponibilité des actions d'alimentation sur la plateforme courante.
/// </summary>
public record GetSystemControlAvailabilityQuery() : IRequest<SystemControlAvailabilityDto>;

/// <summary>
/// Handler pour GetSystemControlAvailabilityQuery.
/// </summary>
public class GetSystemControlAvailabilityQueryHandler
    : IRequestHandler<GetSystemControlAvailabilityQuery, SystemControlAvailabilityDto>
{
    private readonly ISystemControlService _systemControlService;

    public GetSystemControlAvailabilityQueryHandler(ISystemControlService systemControlService)
    {
        _systemControlService = systemControlService;
    }

    public Task<SystemControlAvailabilityDto> Handle(
        GetSystemControlAvailabilityQuery query,
        CancellationToken cancellationToken)
        => Task.FromResult(_systemControlService.GetAvailability());
}
