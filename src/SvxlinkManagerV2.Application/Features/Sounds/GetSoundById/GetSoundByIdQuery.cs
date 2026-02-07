using LanguageExt;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Sounds.GetSoundById;

/// <summary>
/// Query pour récupérer un Sound par son ID
/// </summary>
/// <param name="Id">Identifiant du Sound</param>
public record GetSoundByIdQuery(Guid Id);

/// <summary>
/// Handler pour la query GetSoundByIdQuery
/// </summary>
public static class GetSoundByIdQueryHandler
{
    /// <summary>
    /// Traite la query de récupération d'un Sound par ID
    /// </summary>
    public static async Task<Validation<Error, SoundAggregate>> Handle(
        GetSoundByIdQuery query,
        ISoundRepository repository,
        CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(query.Id, cancellationToken);
    }
}
