using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Tracker du talkgroup courant, alimenté par le flux de logs SVXLink.
/// Thread-safe, singleton.
///
/// Motif reconnu : <c>ReflectorLogic: Selecting TG #240</c>, émis par
/// <c>ReflectorLogic::selectTg</c> quelle que soit l'origine du changement — commande DTMF,
/// QSY du réflecteur, bascule sur un talkgroup prioritaire ou expiration de
/// <c>TG_SELECT_TIMEOUT</c>. C'est ce qui permet d'afficher un talkgroup juste sans avoir à
/// deviner l'issue des commandes envoyées par l'application.
/// </summary>
public class TalkGroupTracker : ITalkGroupStateService, IDisposable
{
    /// <summary>
    /// Le nom de la logique préfixe la ligne : c'est ce qui distingue le message de
    /// <c>ReflectorLogic</c> d'un texte quelconque contenant « Selecting TG ».
    /// </summary>
    private static readonly Regex SelectingTgPattern = new(
        @"ReflectorLogic:\s*Selecting TG #(\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger<TalkGroupTracker> _logger;
    private readonly ISvxLinkLogService _logService;
    private readonly object _lock = new();
    private int? _current;
    private bool _applicable;
    private bool _disposed;

    public event Action<int?>? OnTalkGroupChanged;

    public int? Current
    {
        get { lock (_lock) return _current; }
    }

    public TalkGroupTracker(ILogger<TalkGroupTracker> logger, ISvxLinkLogService logService)
    {
        _logger = logger;
        _logService = logService;

        _logService.OnLogReceived += OnLogReceived;

        _logger.LogInformation("TalkGroupTracker initialisé et abonné aux logs SVXLink");
    }

    public void ApplyDefault(int talkGroup)
    {
        bool changed;
        lock (_lock)
        {
            _applicable = true;
            changed = _current != talkGroup;
            _current = talkGroup;
        }

        _logger.LogInformation("Talkgroup repositionné sur la valeur par défaut du salon : {TalkGroup}", talkGroup);

        if (changed)
            OnTalkGroupChanged?.Invoke(talkGroup);
    }

    public void MarkNotApplicable()
    {
        bool changed;
        lock (_lock)
        {
            _applicable = false;
            changed = _current is not null;
            _current = null;
        }

        _logger.LogInformation("Aucun talkgroup pour le salon actif : suivi désactivé");

        if (changed)
            OnTalkGroupChanged?.Invoke(null);
    }

    private void OnLogReceived(SvxLinkLogEntry entry)
    {
        try
        {
            int? next = null;
            lock (_lock)
            {
                // Salon V2, perroquet ou mode autonome : les lignes résiduelles du daemon
                // ne doivent pas faire apparaître un talkgroup là où il n'y en a pas.
                if (!_applicable)
                    return;

                var match = SelectingTgPattern.Match(entry.Message);
                if (!match.Success)
                    return;

                if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var talkGroup))
                    return;

                if (_current == talkGroup)
                    return;

                _current = talkGroup;
                next = talkGroup;
            }

            _logger.LogInformation("Talkgroup courant : {TalkGroup}", next);
            OnTalkGroupChanged?.Invoke(next);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'analyse d'une ligne de log pour le talkgroup");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _logService.OnLogReceived -= OnLogReceived;
        _logger.LogInformation("TalkGroupTracker dispose - désabonnement des logs SVXLink");

        _disposed = true;
    }
}
