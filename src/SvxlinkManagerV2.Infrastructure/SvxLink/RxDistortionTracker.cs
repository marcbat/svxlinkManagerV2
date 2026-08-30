using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Compte les écrêtages de l'audio en réception signalés par SVXLink.
/// Thread-safe, singleton.
///
/// Motif reconnu, identique en 19.09.2 et 25.05, émis par la classe <c>PeakMeter</c> lorsque
/// <c>PEAK_METER=1</c> est posé sur le récepteur (c'est le cas du modèle svxlink.conf du projet) :
/// <c>Rx1: Distortion detected! Please lower the input volume!</c>
/// </summary>
public class RxDistortionTracker : IRxDistortionService, IDisposable
{
    /// <summary>
    /// Fragment de message discriminant, insensible aux préfixes de nom de récepteur et d'horodatage.
    /// </summary>
    private const string DistortionMarker = "Distortion detected";

    private readonly ILogger<RxDistortionTracker> _logger;
    private readonly ISvxLinkLogService _logService;
    private readonly object _lock = new();

    private DateTimeOffset? _lastDetectedAt;
    private int _detectionCount;
    private bool _disposed;

    public RxDistortionTracker(
        ILogger<RxDistortionTracker> logger,
        ISvxLinkLogService logService)
    {
        _logger = logger;
        _logService = logService;

        _logService.OnLogReceived += OnLogReceived;

        _logger.LogInformation("RxDistortionTracker initialisé et abonné aux logs SVXLink");
    }

    public DateTimeOffset? LastDetectedAt
    {
        get { lock (_lock) return _lastDetectedAt; }
    }

    public int DetectionCount
    {
        get { lock (_lock) return _detectionCount; }
    }

    public event Action<DateTimeOffset>? OnDistortionDetected;

    public void Reset()
    {
        lock (_lock)
        {
            _lastDetectedAt = null;
            _detectionCount = 0;
        }
    }

    private void OnLogReceived(SvxLinkLogEntry entry)
    {
        if (!entry.Message.Contains(DistortionMarker, StringComparison.OrdinalIgnoreCase))
            return;

        DateTimeOffset detectedAt;

        lock (_lock)
        {
            detectedAt = DateTimeOffset.UtcNow;
            _lastDetectedAt = detectedAt;
            _detectionCount++;
        }

        _logger.LogWarning("Saturation de l'audio en réception signalée par SVXLink : {Message}", entry.Message);

        OnDistortionDetected?.Invoke(detectedAt);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logService.OnLogReceived -= OnLogReceived;

        GC.SuppressFinalize(this);
    }
}
