using MediatR;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Application.Features.Salons.Sound;

/// <summary>
/// Query pour récupérer les métadonnées du son d'un salon (sans le binaire)
/// </summary>
public record GetSalonSoundQuery(Guid SalonId) : IRequest<SoundSummaryDto?>;

/// <summary>
/// Handler pour la query GetSalonSoundQuery
/// </summary>
public class GetSalonSoundQueryHandler : IRequestHandler<GetSalonSoundQuery, SoundSummaryDto?>
{
    private readonly ISalonRepository _salonRepository;
    private readonly ISoundRepository _soundRepository;

    public GetSalonSoundQueryHandler(
        ISalonRepository salonRepository,
        ISoundRepository soundRepository)
    {
        _salonRepository = salonRepository;
        _soundRepository = soundRepository;
    }

    public async Task<SoundSummaryDto?> Handle(
        GetSalonSoundQuery query,
        CancellationToken cancellationToken)
    {
        var salonResult = await _salonRepository.GetByIdAsync(query.SalonId, cancellationToken);
        if (salonResult.IsFail)
            return null;

        var salon = salonResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        if (!salon.SoundId.HasValue)
            return null;

        var soundResult = await _soundRepository.GetByIdAsync(salon.SoundId.Value, cancellationToken);
        if (soundResult.IsFail)
            return null;

        var sound = soundResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        return new SoundSummaryDto(
            sound.Id,
            sound.Name,
            sound.Duration,
            sound.SampleRate,
            sound.Channels,
            sound.CreatedAt);
    }
}
