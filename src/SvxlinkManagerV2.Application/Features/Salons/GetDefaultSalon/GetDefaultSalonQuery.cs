using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Application.Features.Salons.GetDefaultSalon;

/// <summary>
/// Query pour récupérer le Salon marqué par défaut
/// </summary>
public record GetDefaultSalonQuery() : IRequest<SalonAggregate?>;

/// <summary>
/// Handler pour la query GetDefaultSalonQuery.
/// Retourne le salon par défaut, ou null si aucun n'est configuré.
/// </summary>
public class GetDefaultSalonQueryHandler : IRequestHandler<GetDefaultSalonQuery, SalonAggregate?>
{
    private readonly ISalonRepository _repository;

    public GetDefaultSalonQueryHandler(ISalonRepository repository)
    {
        _repository = repository;
    }

    public async Task<SalonAggregate?> Handle(
        GetDefaultSalonQuery query,
        CancellationToken cancellationToken)
    {
        return await _repository.GetDefaultAsync(cancellationToken);
    }
}
