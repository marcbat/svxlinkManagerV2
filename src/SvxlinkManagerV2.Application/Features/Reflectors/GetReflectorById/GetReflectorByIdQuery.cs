using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Reflectors.GetReflectorById;

/// <summary>
/// Query pour récupérer un Reflector par son identifiant
/// </summary>
/// <param name="Id">Identifiant unique du reflector</param>
public record GetReflectorByIdQuery(Guid Id) : IRequest<Validation<Error, ReflectorAggregate>>;

/// <summary>
/// Handler pour la query GetReflectorByIdQuery
/// </summary>
public class GetReflectorByIdQueryHandler : IRequestHandler<GetReflectorByIdQuery, Validation<Error, ReflectorAggregate>>
{
    private readonly IReflectorRepository _repository;

    public GetReflectorByIdQueryHandler(IReflectorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Validation<Error, ReflectorAggregate>> Handle(
        GetReflectorByIdQuery query,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(query.Id, cancellationToken);
    }
}
