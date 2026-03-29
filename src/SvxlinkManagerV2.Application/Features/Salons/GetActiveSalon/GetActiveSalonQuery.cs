using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;

/// <summary>
/// Query pour récupérer le Salon actuellement actif
/// </summary>
public record GetActiveSalonQuery() : IRequest<SalonAggregate?>;

/// <summary>
/// Handler pour la query GetActiveSalonQuery
/// </summary>
public class GetActiveSalonQueryHandler : IRequestHandler<GetActiveSalonQuery, SalonAggregate?>
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;

    public GetActiveSalonQueryHandler(ISalonRepository repository, IActiveSessionTracker tracker)
    {
        _repository = repository;
        _tracker = tracker;
    }

    public async Task<SalonAggregate?> Handle(
        GetActiveSalonQuery query,
        CancellationToken cancellationToken)
    {
        var activeSalonId = _tracker.ActiveSalonId;
        if (!activeSalonId.HasValue)
            return null;

        var result = await _repository.GetByIdAsync(activeSalonId.Value, cancellationToken);
        if (result.IsFail) return null;
        var salon = result.Match(Succ: a => a, Fail: _ => throw new InvalidOperationException());
        return salon.IsDeleted ? null : salon;
    }
}
