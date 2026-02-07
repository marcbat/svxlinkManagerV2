using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil;

namespace SvxlinkManagerV2.Application.Features.RadioProfils.GetAllRadioProfils;

/// <summary>
/// Query pour récupérer tous les RadioProfils
/// </summary>
public record GetAllRadioProfilsQuery();

/// <summary>
/// Handler pour la query GetAllRadioProfilsQuery
/// </summary>
public static class GetAllRadioProfilsQueryHandler
{
    /// <summary>
    /// Traite la query de récupération de tous les RadioProfils
    /// </summary>
    public static async Task<IReadOnlyList<RadioProfilAggregate>> Handle(
        GetAllRadioProfilsQuery query,
        IRadioProfilRepository repository,
        CancellationToken cancellationToken)
    {
        return await repository.GetAllAsync(cancellationToken);
    }
}
