using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.Hardware;

/// <summary>
/// Implémentation simulée des niveaux ALSA, pour développer la page audio sans carte son.
/// Les plages reproduisent celles du codec H3 de l'Orange Pi Zero : 0-7 pour le gain d'ADC,
/// 0-31 pour la sortie ligne. L'état est conservé en mémoire pour la durée du processus.
/// </summary>
public class AudioMockService : IAudioService
{
    private const int MockCaptureMax = 7;
    private const int MockPlaybackMax = 31;

    private readonly ILogger<AudioMockService> _logger;
    private readonly AudioOptions _options;
    private readonly object _gate = new();

    private int _captureLevel = 3;
    private int _playbackLevel = 22;

    public AudioMockService(IOptions<AudioOptions> options, ILogger<AudioMockService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsSimulated => true;

    public Task<Validation<Error, AudioMixerState>> GetStateAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var state = new AudioMixerState(
                _options.CardIndex,
                new AudioControlState(_options.CaptureControl, _captureLevel, 0, MockCaptureMax),
                new AudioControlState(_options.PlaybackControl, _playbackLevel, 0, MockPlaybackMax),
                IsSimulated);

            return Task.FromResult(state.ToSuccess());
        }
    }

    public Task<Validation<Error, AudioControlState>> SetCaptureLevelAsync(int value, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _captureLevel = Math.Clamp(value, 0, MockCaptureMax);
            _logger.LogInformation("MOCK: niveau de capture réglé sur {Value}", _captureLevel);

            return Task.FromResult(
                new AudioControlState(_options.CaptureControl, _captureLevel, 0, MockCaptureMax).ToSuccess());
        }
    }

    public Task<Validation<Error, AudioControlState>> SetPlaybackLevelAsync(int value, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _playbackLevel = Math.Clamp(value, 0, MockPlaybackMax);
            _logger.LogInformation("MOCK: niveau de restitution réglé sur {Value}", _playbackLevel);

            return Task.FromResult(
                new AudioControlState(_options.PlaybackControl, _playbackLevel, 0, MockPlaybackMax).ToSuccess());
        }
    }
}
