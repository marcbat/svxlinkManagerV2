using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;

namespace SvxlinkManagerV2.Application.Features.Reflectors.GetAllReflectors;

/// <summary>
/// Query pour récupérer tous les Reflectors
/// </summary>
public record GetAllReflectorsQuery();

/// <summary>
/// Handler pour la query GetAllReflectorsQuery
/// </summary>
public static class GetAllReflectorsQueryHandler
{
    /// <summary>
    /// Retourne tous les Reflectors non supprimés
    /// </summary>
    public static async Task<IReadOnlyList<ReflectorAggregate>> Handle(
        GetAllReflectorsQuery query,
        IReflectorRepository repository,
        CancellationToken cancellationToken)
    {
        return await repository.GetAllAsync(cancellationToken);
    }
}
