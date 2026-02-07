using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.GetSalonById;

/// <summary>
/// Query pour récupérer un Salon par son identifiant
/// </summary>
/// <param name="Id">Identifiant unique du salon</param>
public record GetSalonByIdQuery(Guid Id);

/// <summary>
/// Handler pour la query GetSalonByIdQuery
/// </summary>
public static class GetSalonByIdQueryHandler
{
    /// <summary>
    /// Traite la query de récupération d'un Salon par ID
    /// </summary>
    public static async Task<Validation<Error, SalonAggregate>> Handle(
        GetSalonByIdQuery query,
        ISalonRepository repository,
        CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(query.Id, cancellationToken);
    }
}
