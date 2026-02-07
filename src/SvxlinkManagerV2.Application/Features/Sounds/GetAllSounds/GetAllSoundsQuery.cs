using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;

namespace SvxlinkManagerV2.Application.Features.Sounds.GetAllSounds;

/// <summary>
/// Query pour récupérer tous les Sounds
/// </summary>
public record GetAllSoundsQuery();

/// <summary>
/// Handler pour la query GetAllSoundsQuery
/// </summary>
public static class GetAllSoundsQueryHandler
{
    /// <summary>
    /// Traite la query de récupération de tous les Sounds
    /// </summary>
    public static async Task<IReadOnlyList<SoundAggregate>> Handle(
        GetAllSoundsQuery query,
        ISoundRepository repository,
        CancellationToken cancellationToken)
    {
        return await repository.GetAllAsync(cancellationToken);
    }
}
