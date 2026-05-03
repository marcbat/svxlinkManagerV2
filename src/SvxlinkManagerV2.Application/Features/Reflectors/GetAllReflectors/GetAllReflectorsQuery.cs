using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;

namespace SvxlinkManagerV2.Application.Features.Reflectors.GetAllReflectors;

/// <summary>
/// Query pour récupérer tous les Reflectors
/// </summary>
public record GetAllReflectorsQuery() : IRequest<IReadOnlyList<ReflectorAggregate>>;

/// <summary>
/// Handler pour la query GetAllReflectorsQuery
/// </summary>
public class GetAllReflectorsQueryHandler : IRequestHandler<GetAllReflectorsQuery, IReadOnlyList<ReflectorAggregate>>
{
    private readonly IReflectorRepository _repository;

    public GetAllReflectorsQueryHandler(IReflectorRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ReflectorAggregate>> Handle(
        GetAllReflectorsQuery query,
        CancellationToken cancellationToken)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }
}
