using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Application.Features.Salons.GetActiveSalon;

/// <summary>
/// Query pour récupérer le Salon actuellement actif
/// </summary>
public record GetActiveSalonQuery();

/// <summary>
/// Handler pour la query GetActiveSalonQuery
/// </summary>
public static class GetActiveSalonQueryHandler
{
    /// <summary>
    /// Traite la query de récupération du Salon actif
    /// </summary>
    public static async Task<SalonAggregate?> Handle(
        GetActiveSalonQuery query,
        ISalonRepository repository,
        CancellationToken cancellationToken)
    {
        return await repository.GetActiveAsync(cancellationToken);
    }
}
