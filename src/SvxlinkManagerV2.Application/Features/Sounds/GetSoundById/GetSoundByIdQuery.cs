using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Sounds.GetSoundById;

/// <summary>
/// Query pour récupérer un Sound par son ID
/// </summary>
/// <param name="Id">Identifiant du Sound</param>
public record GetSoundByIdQuery(Guid Id) : IRequest<Validation<Error, SoundAggregate>>;

/// <summary>
/// Handler pour la query GetSoundByIdQuery
/// </summary>
public class GetSoundByIdQueryHandler : IRequestHandler<GetSoundByIdQuery, Validation<Error, SoundAggregate>>
{
    private readonly ISoundRepository _repository;

    public GetSoundByIdQueryHandler(ISoundRepository repository)
    {
        _repository = repository;
    }

    public async Task<Validation<Error, SoundAggregate>> Handle(
        GetSoundByIdQuery query,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(query.Id, cancellationToken);
    }
}
