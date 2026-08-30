using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Audio.GetAudioSettings;

/// <summary>
/// Query retournant l'état complet de la page de réglage audio.
/// </summary>
public record GetAudioSettingsQuery() : IRequest<Validation<Error, AudioSettingsDto>>;

/// <summary>
/// Handler de <see cref="GetAudioSettingsQuery"/>.
/// Une carte son illisible n'est pas un échec de la query : le motif est porté par le DTO, afin
/// que le test d'émission reste accessible même si les niveaux ne peuvent pas être lus.
/// </summary>
public class GetAudioSettingsQueryHandler
    : IRequestHandler<GetAudioSettingsQuery, Validation<Error, AudioSettingsDto>>
{
    private readonly IAudioService _audioService;
    private readonly IPttTestService _pttTestService;
    private readonly IActiveSessionTracker _tracker;
    private readonly ISvxLinkDaemonService _daemonService;

    public GetAudioSettingsQueryHandler(
        IAudioService audioService,
        IPttTestService pttTestService,
        IActiveSessionTracker tracker,
        ISvxLinkDaemonService daemonService)
    {
        _audioService = audioService;
        _pttTestService = pttTestService;
        _tracker = tracker;
        _daemonService = daemonService;
    }

    public async Task<Validation<Error, AudioSettingsDto>> Handle(
        GetAudioSettingsQuery query,
        CancellationToken cancellationToken)
    {
        var mixerResult = await _audioService.GetStateAsync(cancellationToken);
        var blockedReason = await PttTestAvailability.GetBlockedReasonAsync(_tracker, _daemonService, cancellationToken);
        var pttState = _pttTestService.State;

        var dto = mixerResult.Match(
            Succ: mixer => new AudioSettingsDto
            {
                Capture = ToLevelDto(mixer.Capture),
                Playback = ToLevelDto(mixer.Playback),
                IsSimulated = mixer.IsSimulated,
                Ptt = ToStatusDto(pttState, blockedReason),
                DefaultTestDurationSeconds = _pttTestService.DefaultDurationSeconds,
                MaxTestDurationSeconds = _pttTestService.MaxDurationSeconds
            },
            Fail: errors => new AudioSettingsDto
            {
                LevelsError = string.Join(" ", errors.Select(error => error.Message)),
                IsSimulated = _audioService.IsSimulated,
                Ptt = ToStatusDto(pttState, blockedReason),
                DefaultTestDurationSeconds = _pttTestService.DefaultDurationSeconds,
                MaxTestDurationSeconds = _pttTestService.MaxDurationSeconds
            });

        return dto.ToSuccess();
    }

    internal static AudioLevelDto ToLevelDto(AudioControlState control) =>
        new(control.Name, control.Value, control.MinValue, control.MaxValue, control.Percent);

    internal static PttTestStatusDto ToStatusDto(PttTestState state, string? blockedReason) =>
        new(
            state.IsTransmitting,
            state.RemainingSeconds,
            state.IsSimulated,
            CanStart: blockedReason is null && !state.IsTransmitting,
            BlockedReason: blockedReason);
}
