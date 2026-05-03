using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.GetSalonById;

/// <summary>
/// Query pour récupérer un Salon par son identifiant
/// </summary>
/// <param name="Id">Identifiant unique du salon</param>
public record GetSalonByIdQuery(Guid Id) : IRequest<Validation<Error, SalonAggregate>>;

/// <summary>
/// Handler pour la query GetSalonByIdQuery
/// </summary>
public class GetSalonByIdQueryHandler : IRequestHandler<GetSalonByIdQuery, Validation<Error, SalonAggregate>>
{
    private readonly ISalonRepository _repository;

    public GetSalonByIdQueryHandler(ISalonRepository repository)
    {
        _repository = repository;
    }

    public async Task<Validation<Error, SalonAggregate>> Handle(
        GetSalonByIdQuery query,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(query.Id, cancellationToken);
    }
}
