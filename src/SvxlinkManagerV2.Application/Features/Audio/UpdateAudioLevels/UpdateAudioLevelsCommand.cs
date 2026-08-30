using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Features.Audio.GetAudioSettings;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.Audio.UpdateAudioLevels;

/// <summary>
/// Commande appliquant de nouveaux niveaux à la carte son, puis les mémorisant.
/// </summary>
/// <param name="CaptureLevel">Niveau brut souhaité pour le contrôle de capture.</param>
/// <param name="PlaybackLevel">Niveau brut souhaité pour le contrôle de restitution.</param>
public record UpdateAudioLevelsCommand(int CaptureLevel, int PlaybackLevel)
    : IRequest<Validation<Error, AudioLevelsDto>>;

/// <summary>
/// Handler de <see cref="UpdateAudioLevelsCommand"/>.
///
/// L'ordre compte : la carte son est réglée d'abord, la mémorisation ensuite. Ce sont les valeurs
/// effectivement retenues par le pilote qui sont enregistrées — bornées à la plage réelle du
/// contrôle — et non celles demandées, pour que la base reflète l'état réel du matériel.
/// </summary>
public class UpdateAudioLevelsCommandHandler
    : IRequestHandler<UpdateAudioLevelsCommand, Validation<Error, AudioLevelsDto>>
{
    private readonly IAudioService _audioService;
    private readonly IAudioConfigurationRepository _repository;

    public UpdateAudioLevelsCommandHandler(
        IAudioService audioService,
        IAudioConfigurationRepository repository)
    {
        _audioService = audioService;
        _repository = repository;
    }

    public async Task<Validation<Error, AudioLevelsDto>> Handle(
        UpdateAudioLevelsCommand command,
        CancellationToken cancellationToken)
    {
        var captureResult = await _audioService.SetCaptureLevelAsync(command.CaptureLevel, cancellationToken);
        var playbackResult = await _audioService.SetPlaybackLevelAsync(command.PlaybackLevel, cancellationToken);

        var applied = (captureResult, playbackResult).Apply(
            (capture, playback) => (Capture: capture, Playback: playback));

        if (applied.IsFail)
            return applied.Map(_ => default(AudioLevelsDto)!);

        var levels = applied.Match(
            Succ: value => value,
            Fail: _ => throw new InvalidOperationException("Application des niveaux déjà validée."));

        var persistResult = await PersistAsync(levels.Capture, levels.Playback, cancellationToken);
        if (persistResult.IsFail)
            return persistResult.Map(_ => default(AudioLevelsDto)!);

        return new AudioLevelsDto(
            GetAudioSettingsQueryHandler.ToLevelDto(levels.Capture),
            GetAudioSettingsQueryHandler.ToLevelDto(levels.Playback)).ToSuccess();
    }

    /// <summary>
    /// Mémorise les niveaux réellement appliqués, en créant l'aggregate au besoin.
    /// </summary>
    private async Task<Validation<Error, Unit>> PersistAsync(
        AudioControlState capture,
        AudioControlState playback,
        CancellationToken cancellationToken)
    {
        var existing = (await _repository.GetAsync(cancellationToken)).SuccessOrNull();

        if (existing is not null)
        {
            var updateResult = existing.UpdateLevels(
                capture.Name, capture.Value, playback.Name, playback.Value);

            if (updateResult.IsFail)
                return updateResult;

            return await _repository.SaveAsync(existing, cancellationToken);
        }

        var createResult = AudioConfigurationAggregate.Create(
            capture.Name, capture.Value, playback.Name, playback.Value);

        return await createResult.Match(
            Succ: aggregate => _repository.SaveAsync(aggregate, cancellationToken),
            Fail: errors => Task.FromResult(errors.ToFailure<Unit>()));
    }
}
