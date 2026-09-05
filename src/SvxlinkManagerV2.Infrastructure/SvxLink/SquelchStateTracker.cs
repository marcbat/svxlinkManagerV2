using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Suit les ouvertures du squelch du récepteur local en guettant le flux de logs SVXLink.
/// Thread-safe, singleton.
///
/// Motif reconnu, identique en 19.09.2 et 25.05, émis par <c>LocalRxBase</c> à chaque bascule :
/// <c>Rx1: The squelch is OPEN (12.3)</c> puis <c>Rx1: The squelch is CLOSED (-1.2)</c>.
/// Le nom du récepteur et le niveau entre parenthèses varient, la phrase non : la détection
/// ne porte que sur elle.
///
/// C'est la seule mesure d'activité radio locale accessible à l'application. Le périphérique
/// de capture ALSA est ouvert en exclusivité par SVXLink dès qu'un salon tourne, et en mode
/// autonome comme sur un salon perroquet il n'existe aucune liaison réflecteur pour rapporter
/// les passages : sans le squelch, ces périodes seraient muettes.
///
/// Si la configuration de SVXLink ne produit pas ces lignes, le tracker reste simplement
/// silencieux — jamais en erreur.
/// </summary>
public class SquelchStateTracker : ISquelchStateService, IDisposable
{
    /// <summary>Fragment discriminant d'une ouverture, insensible au nom du récepteur.</summary>
    private const string OpenMarker = "The squelch is OPEN";

    /// <summary>Fragment discriminant d'une fermeture.</summary>
    private const string ClosedMarker = "The squelch is CLOSED";

    private readonly ILogger<SquelchStateTracker> _logger;
    private readonly ISvxLinkLogService _logService;
    private readonly object _lock = new();

    private DateTimeOffset? _openedAt;
    private bool _disposed;

    public event Action<DateTimeOffset>? OnSquelchOpened;
    public event Action<TimeSpan>? OnSquelchClosed;

    public SquelchStateTracker(
        ILogger<SquelchStateTracker> logger,
        ISvxLinkLogService logService)
    {
        _logger = logger;
        _logService = logService;

        _logService.OnLogReceived += OnLogReceived;

        _logger.LogInformation("SquelchStateTracker initialisé et abonné aux logs SVXLink");
    }

    public bool IsOpen
    {
        get { lock (_lock) return _openedAt.HasValue; }
    }

    private void OnLogReceived(SvxLinkLogEntry entry)
    {
        try
        {
            if (entry.Message.Contains(OpenMarker, StringComparison.OrdinalIgnoreCase))
            {
                Open();
                return;
            }

            if (entry.Message.Contains(ClosedMarker, StringComparison.OrdinalIgnoreCase))
                Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'analyse de l'état du squelch : {Message}", entry.Message);
        }
    }

    private void Open()
    {
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            // Une seconde ouverture sans fermeture ne peut venir que d'une ligne rejouée
            // ou d'un second récepteur : la première ouverture fait foi.
            if (_openedAt.HasValue)
                return;

            _openedAt = now;
        }

        _logger.LogDebug("Squelch ouvert");
        OnSquelchOpened?.Invoke(now);
    }

    private void Close()
    {
        TimeSpan duration;

        lock (_lock)
        {
            // Fermeture sans ouverture connue : l'application a démarré alors que le squelch
            // était déjà ouvert. Aucune durée n'est mesurable, l'événement est ignoré.
            if (_openedAt is not { } openedAt)
                return;

            duration = DateTimeOffset.UtcNow - openedAt;
            _openedAt = null;
        }

        _logger.LogDebug("Squelch fermé après {Seconds:F1} s", duration.TotalSeconds);
        OnSquelchClosed?.Invoke(duration);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _logService.OnLogReceived -= OnLogReceived;
        _logger.LogInformation("SquelchStateTracker dispose - désabonnement des logs SVXLink");

        _disposed = true;
    }
}
