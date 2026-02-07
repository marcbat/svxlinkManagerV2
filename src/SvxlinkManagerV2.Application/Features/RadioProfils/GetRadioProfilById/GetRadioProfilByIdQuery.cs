using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.RadioProfil;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.RadioProfils.GetRadioProfilById;

/// <summary>
/// Query pour récupérer un RadioProfil par son identifiant
/// </summary>
/// <param name="Id">Identifiant du profil à récupérer</param>
public record GetRadioProfilByIdQuery(Guid Id);

/// <summary>
/// Handler pour la query GetRadioProfilByIdQuery
/// </summary>
public static class GetRadioProfilByIdQueryHandler
{
    /// <summary>
    /// Traite la query de récupération d'un RadioProfil par ID
    /// </summary>
    public static async Task<Validation<Error, RadioProfilAggregate>> Handle(
        GetRadioProfilByIdQuery query,
        IRadioProfilRepository repository,
        CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(query.Id, cancellationToken);
    }
}
