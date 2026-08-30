using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.Hardware;

/// <summary>
/// Machine à états commune aux tests d'émission réel et simulé : durée bornée, relâchement
/// automatique à l'échéance, arrêt manuel immédiat, et relâchement systématique à l'arrêt
/// de l'application. Seule l'action sur le PTT est laissée aux implémentations.
/// </summary>
public abstract class PttTestServiceBase : IPttTestService, IDisposable
{
    private readonly ILogger _logger;
    private readonly object _gate = new();

    private Timer? _releaseTimer;
    private DateTimeOffset? _endsAt;
    private bool _isTransmitting;
    private bool _disposed;

    protected PttTestServiceBase(IOptions<AudioOptions> options, ILogger logger)
    {
        Options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Options de la chaîne audio, dont les durées de test.
    /// </summary>
    protected AudioOptions Options { get; }

    /// <summary>
    /// Vrai lorsque le PTT est simulé (développement sans matériel).
    /// </summary>
    protected abstract bool IsSimulated { get; }

    public int DefaultDurationSeconds => Math.Clamp(Options.PttTestDurationSeconds, 1, MaxDurationSeconds);

    public int MaxDurationSeconds => Math.Max(1, Options.PttTestMaxDurationSeconds);

    public PttTestState State
    {
        get
        {
            lock (_gate)
            {
                return new PttTestState(_isTransmitting, _endsAt, IsSimulated);
            }
        }
    }

    public event Action<PttTestState>? OnStateChanged;

    public async Task<Validation<Error, PttTestState>> StartAsync(
        int durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var maxDuration = MaxDurationSeconds;

        if (durationSeconds < 1)
        {
            return Error.Validation(
                    "PTT_TEST_DURATION_INVALID",
                    "La durée du test d'émission doit être d'au moins une seconde.")
                .ToFailure<PttTestState>();
        }

        if (durationSeconds > maxDuration)
        {
            return Error.Validation(
                    "PTT_TEST_DURATION_TOO_LONG",
                    $"La durée du test d'émission ne peut pas dépasser {maxDuration} secondes.")
                .ToFailure<PttTestState>();
        }

        lock (_gate)
        {
            if (_isTransmitting)
            {
                return Error.Conflict("Un test d'émission est déjà en cours.")
                    .ToFailure<PttTestState>();
            }
        }

        var keyResult = await SetPttAsync(true, cancellationToken);
        if (keyResult.IsFail)
            return keyResult.Map(_ => State);

        PttTestState state;

        lock (_gate)
        {
            _isTransmitting = true;
            _endsAt = DateTimeOffset.UtcNow.AddSeconds(durationSeconds);

            // Le relâchement est porté par un minuteur du singleton, jamais par le circuit Blazor
            // appelant : fermer l'onglet ne doit pas laisser la station en émission.
            _releaseTimer?.Dispose();
            _releaseTimer = new Timer(
                _ => _ = ReleaseAsync("échéance du minuteur"),
                null,
                TimeSpan.FromSeconds(durationSeconds),
                Timeout.InfiniteTimeSpan);

            state = new PttTestState(_isTransmitting, _endsAt, IsSimulated);
        }

        _logger.LogWarning("Test d'émission démarré pour {Duration} seconde(s)", durationSeconds);
        RaiseStateChanged(state);

        return state.ToSuccess();
    }

    public async Task<Validation<Error, PttTestState>> StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_isTransmitting)
                return State.ToSuccess();
        }

        return await ReleaseAsync("arrêt manuel", cancellationToken);
    }

    /// <summary>
    /// Relâche le PTT et notifie le changement d'état.
    /// </summary>
    /// <param name="reason">Motif journalisé du relâchement.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    private async Task<Validation<Error, PttTestState>> ReleaseAsync(
        string reason,
        CancellationToken cancellationToken = default)
    {
        var result = await SetPttAsync(false, cancellationToken);

        PttTestState state;

        lock (_gate)
        {
            _releaseTimer?.Dispose();
            _releaseTimer = null;
            _isTransmitting = false;
            _endsAt = null;
            state = new PttTestState(false, null, IsSimulated);
        }

        if (result.IsFail)
        {
            _logger.LogError("Échec du relâchement du PTT ({Reason})", reason);
        }
        else
        {
            _logger.LogWarning("Test d'émission terminé ({Reason})", reason);
        }

        RaiseStateChanged(state);

        return result.Map(_ => state);
    }

    /// <summary>
    /// Commande physique du PTT.
    /// </summary>
    /// <param name="keyed">Vrai pour passer en émission, faux pour relâcher.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    protected abstract Task<Validation<Error, Unit>> SetPttAsync(bool keyed, CancellationToken cancellationToken);

    private void RaiseStateChanged(PttTestState state)
    {
        try
        {
            OnStateChanged?.Invoke(state);
        }
        catch (Exception ex)
        {
            // Un abonné en erreur (circuit Blazor fermé) ne doit pas compromettre le PTT.
            _logger.LogWarning(ex, "Un abonné à l'état du test d'émission a levé une exception");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Relâche le PTT si l'application s'arrête pendant un test.
    /// </summary>
    /// <param name="disposing">Vrai lors d'une libération déterministe.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
            return;

        _disposed = true;

        bool wasTransmitting;
        lock (_gate)
        {
            wasTransmitting = _isTransmitting;
            _releaseTimer?.Dispose();
            _releaseTimer = null;
            _isTransmitting = false;
            _endsAt = null;
        }

        if (wasTransmitting)
        {
            _logger.LogWarning("Arrêt de l'application pendant un test d'émission : relâchement du PTT");
            SetPttAsync(false, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
