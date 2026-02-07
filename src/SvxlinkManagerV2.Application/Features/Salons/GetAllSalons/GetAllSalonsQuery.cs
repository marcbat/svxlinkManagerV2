using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Application.Features.Salons.GetAllSalons;

/// <summary>
/// Query pour récupérer tous les Salons
/// </summary>
public record GetAllSalonsQuery();

/// <summary>
/// Handler pour la query GetAllSalonsQuery
/// </summary>
public static class GetAllSalonsQueryHandler
{
    /// <summary>
    /// Traite la query de récupération de tous les Salons
    /// </summary>
    public static async Task<IReadOnlyList<SalonAggregate>> Handle(
        GetAllSalonsQuery query,
        ISalonRepository repository,
        CancellationToken cancellationToken)
    {
        return await repository.GetAllAsync(cancellationToken);
    }
}
