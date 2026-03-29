using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Application.Features.Salons.GetAllSalons;

/// <summary>
/// Query pour récupérer tous les Salons
/// </summary>
public record GetAllSalonsQuery() : IRequest<IReadOnlyList<SalonAggregate>>;

/// <summary>
/// Handler pour la query GetAllSalonsQuery
/// </summary>
public class GetAllSalonsQueryHandler : IRequestHandler<GetAllSalonsQuery, IReadOnlyList<SalonAggregate>>
{
    private readonly ISalonRepository _repository;

    public GetAllSalonsQueryHandler(ISalonRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<SalonAggregate>> Handle(
        GetAllSalonsQuery query,
        CancellationToken cancellationToken)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }
}
