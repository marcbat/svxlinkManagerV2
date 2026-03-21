using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Reflectors.GetReflectorById;

/// <summary>
/// Query pour récupérer un Reflector par son identifiant
/// </summary>
/// <param name="Id">Identifiant unique du reflector</param>
public record GetReflectorByIdQuery(Guid Id);

/// <summary>
/// Handler pour la query GetReflectorByIdQuery
/// </summary>
public static class GetReflectorByIdQueryHandler
{
    /// <summary>
    /// Retourne le Reflector correspondant à l'identifiant, ou une erreur NotFound
    /// </summary>
    public static async Task<Validation<Error, ReflectorAggregate>> Handle(
        GetReflectorByIdQuery query,
        IReflectorRepository repository,
        CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(query.Id, cancellationToken);
    }
}
