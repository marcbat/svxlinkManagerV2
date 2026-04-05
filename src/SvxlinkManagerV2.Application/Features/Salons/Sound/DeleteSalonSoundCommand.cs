using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.Sound;

/// <summary>
/// Commande pour supprimer le son d'un salon
/// </summary>
public record DeleteSalonSoundCommand(Guid SalonId) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande DeleteSalonSoundCommand
/// </summary>
public class DeleteSalonSoundCommandHandler : IRequestHandler<DeleteSalonSoundCommand, Validation<Error, Unit>>
{
    private readonly ISalonRepository _salonRepository;
    private readonly ISoundRepository _soundRepository;
    private readonly IActiveSessionTracker _tracker;

    public DeleteSalonSoundCommandHandler(
        ISalonRepository salonRepository,
        ISoundRepository soundRepository,
        IActiveSessionTracker tracker)
    {
        _salonRepository = salonRepository;
        _soundRepository = soundRepository;
        _tracker = tracker;
    }

    public async Task<Validation<Error, Unit>> Handle(
        DeleteSalonSoundCommand command,
        CancellationToken cancellationToken)
    {
        if (_tracker.IsSalonActive(command.SalonId))
            return Error.Validation("SALON_ACTIVE", "Impossible de modifier le son d'un salon actif").ToFailure<Unit>();

        var salonResult = await _salonRepository.GetByIdAsync(command.SalonId, cancellationToken);
        if (salonResult.IsFail)
            return salonResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var salon = salonResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        if (!salon.SoundId.HasValue)
            return Error.Validation("SALON_NO_SOUND", "Le salon n'a pas de son à supprimer").ToFailure<Unit>();

        var deleteResult = await _soundRepository.HardDeleteAsync(salon.SoundId.Value, cancellationToken);
        if (deleteResult.IsFail)
            return deleteResult;

        var removeResult = salon.RemoveSound();
        if (removeResult.IsFail)
            return removeResult;

        return await _salonRepository.SaveAsync(salon, cancellationToken);
    }
}
