using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Sound;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Salons.Sound;

/// <summary>
/// Commande pour uploader un son sur un salon
/// </summary>
public record UploadSalonSoundCommand(
    Guid SalonId,
    string Name,
    byte[] FileContent) : IRequest<Validation<Error, Guid>>;

/// <summary>
/// Handler pour la commande UploadSalonSoundCommand
/// </summary>
public class UploadSalonSoundCommandHandler : IRequestHandler<UploadSalonSoundCommand, Validation<Error, Guid>>
{
    private readonly ISalonRepository _salonRepository;
    private readonly ISoundRepository _soundRepository;
    private readonly IActiveSessionTracker _tracker;

    public UploadSalonSoundCommandHandler(
        ISalonRepository salonRepository,
        ISoundRepository soundRepository,
        IActiveSessionTracker tracker)
    {
        _salonRepository = salonRepository;
        _soundRepository = soundRepository;
        _tracker = tracker;
    }

    public async Task<Validation<Error, Guid>> Handle(
        UploadSalonSoundCommand command,
        CancellationToken cancellationToken)
    {
        if (_tracker.IsSalonActive(command.SalonId))
            return Error.Validation("SALON_ACTIVE", "Impossible de modifier le son d'un salon actif").ToFailure<Guid>();

        var salonResult = await _salonRepository.GetByIdAsync(command.SalonId, cancellationToken);
        if (salonResult.IsFail)
            return salonResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Guid>.Fail(errors));

        var salon = salonResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        if (salon.SoundId.HasValue)
            return Error.Validation("SALON_SOUND_EXISTS", "Le salon a déjà un son. Utilisez la commande de remplacement.").ToFailure<Guid>();

        var soundId = Guid.NewGuid();
        var soundResult = SoundAggregate.Create(soundId, command.Name, command.FileContent);
        if (soundResult.IsFail)
            return soundResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Guid>.Fail(errors));

        var sound = soundResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        var saveSoundResult = await _soundRepository.SaveAsync(sound, cancellationToken);
        if (saveSoundResult.IsFail)
            return saveSoundResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Guid>.Fail(errors));

        var assignResult = salon.AssignSound(soundId);
        if (assignResult.IsFail)
            return assignResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Guid>.Fail(errors));

        var saveSalonResult = await _salonRepository.SaveAsync(salon, cancellationToken);
        return saveSalonResult.Match(
            Succ: _ => Validation<Error, Guid>.Success(soundId),
            Fail: errors => Validation<Error, Guid>.Fail(errors));
    }
}
