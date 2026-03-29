using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;

namespace SvxlinkManagerV2.Application.Features.Sounds.GetAllSounds;

/// <summary>
/// Query pour récupérer tous les Sounds
/// </summary>
public record GetAllSoundsQuery() : IRequest<IReadOnlyList<SoundAggregate>>;

/// <summary>
/// Handler pour la query GetAllSoundsQuery
/// </summary>
public class GetAllSoundsQueryHandler : IRequestHandler<GetAllSoundsQuery, IReadOnlyList<SoundAggregate>>
{
    private readonly ISoundRepository _repository;

    public GetAllSoundsQueryHandler(ISoundRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<SoundAggregate>> Handle(
        GetAllSoundsQuery query,
        CancellationToken cancellationToken)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }
}
