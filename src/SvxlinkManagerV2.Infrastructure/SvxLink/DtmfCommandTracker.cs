using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Tracker des commandes DTMF reçues via les logs SVXLink.
/// Parse les logs en temps réel pour détecter les lignes "DTMF_CMD:xxx" émises par Logic.tcl.
/// Thread-safe, singleton.
/// </summary>
public class DtmfCommandTracker : IDtmfCommandTracker, IDisposable
{
    private const string DtmfCommandPrefix = "DTMF_CMD:";

    private readonly ILogger<DtmfCommandTracker> _logger;
    private readonly ISvxLinkLogService _logService;
    private bool _disposed;

    public event Action<string>? OnDtmfCommandReceived;

    public DtmfCommandTracker(
        ILogger<DtmfCommandTracker> logger,
        ISvxLinkLogService logService)
    {
        _logger = logger;
        _logService = logService;

        _logService.OnLogReceived += OnLogReceived;

        _logger.LogInformation("DtmfCommandTracker initialisé et abonné aux logs SVXLink");
    }

    private void OnLogReceived(SvxLinkLogEntry entry)
    {
        var message = entry.Message;

        // Chercher le pattern "DTMF_CMD:" dans le message
        var index = message.IndexOf(DtmfCommandPrefix, StringComparison.Ordinal);
        if (index < 0)
            return;

        var command = message[(index + DtmfCommandPrefix.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(command))
        {
            _logger.LogWarning("Commande DTMF vide détectée dans le log : {Message}", message);
            return;
        }

        _logger.LogInformation("Commande DTMF détectée : {Command}", command);
        OnDtmfCommandReceived?.Invoke(command);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _logService.OnLogReceived -= OnLogReceived;
        _logger.LogInformation("DtmfCommandTracker dispose - désabonnement des logs SVXLink");

        _disposed = true;
    }
}
