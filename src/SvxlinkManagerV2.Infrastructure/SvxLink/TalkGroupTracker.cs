using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Tracker de l'état des talkgroups, alimenté par le flux de logs SVXLink.
/// Thread-safe, singleton.
/// </summary>
/// <remarks>
/// <para>
/// La source principale est l'instrumentation TCL de <c>Logic.tcl</c>, qui enveloppe les
/// procédures d'événement de <c>ReflectorLogic</c> et émet des lignes <c>TG_EVENT:</c>.
/// Ces procédures sont une interface stable de SVXLink, là où les libellés de log changent
/// d'une version à l'autre — et le projet en pilote deux.
/// </para>
/// <para>
/// La ligne <c>ReflectorLogic: Selecting TG #&lt;n&gt;</c>, émise par le C++, est conservée
/// en repli : elle seule reste disponible sur un nœud dont le <c>Logic.tcl</c> n'a pas encore
/// été redéployé. Les deux sources décrivent le même appel à <c>selectTg</c> et ne peuvent
/// donc pas se contredire ; le repli n'apporte simplement ni l'origine ni les surveillances.
/// </para>
/// </remarks>
public class TalkGroupTracker : ITalkGroupStateService, IDisposable
{
    /// <summary>Préfixe des lignes émises par l'instrumentation TCL.</summary>
    private const string EventPrefix = "TG_EVENT:";

    /// <summary>
    /// Repli sur le message du C++. Le nom de la logique préfixe la ligne : c'est ce qui
    /// distingue le message de <c>ReflectorLogic</c> d'un texte quelconque.
    /// </summary>
    private static readonly Regex SelectingTgPattern = new(
        @"ReflectorLogic:\s*Selecting TG #(\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger<TalkGroupTracker> _logger;
    private readonly ISvxLinkLogService _logService;
    private readonly object _lock = new();
    private TalkGroupState _state = TalkGroupState.NotApplicable;
    private bool _applicable;
    private bool _disposed;

    public event Action<TalkGroupState>? OnTalkGroupChanged;

    public TalkGroupState State
    {
        get { lock (_lock) return _state; }
    }

    public int? Current => State.TalkGroup;

    public TalkGroupTracker(ILogger<TalkGroupTracker> logger, ISvxLinkLogService logService)
    {
        _logger = logger;
        _logService = logService;

        _logService.OnLogReceived += OnLogReceived;

        _logger.LogInformation("TalkGroupTracker initialisé et abonné aux logs SVXLink");
    }

    public void ApplyDefault(int talkGroup)
    {
        TalkGroupState? published = null;
        lock (_lock)
        {
            _applicable = true;

            // Tout est remis à plat : les surveillances temporaires et le QSY en attente
            // appartiennent au daemon qui s'arrête, pas au salon qui démarre.
            var next = new TalkGroupState(talkGroup, Origin: TalkGroupActivationOrigin.Default);
            if (!_state.Equals(next))
            {
                _state = next;
                published = next;
            }
        }

        _logger.LogInformation("Talkgroup repositionné sur la valeur par défaut du salon : {TalkGroup}", talkGroup);

        if (published is not null)
            OnTalkGroupChanged?.Invoke(published);
    }

    public void MarkNotApplicable()
    {
        TalkGroupState? published = null;
        lock (_lock)
        {
            _applicable = false;
            if (!_state.Equals(TalkGroupState.NotApplicable))
            {
                _state = TalkGroupState.NotApplicable;
                published = _state;
            }
        }

        _logger.LogInformation("Aucun talkgroup pour le salon actif : suivi désactivé");

        if (published is not null)
            OnTalkGroupChanged?.Invoke(published);
    }

    private void OnLogReceived(SvxLinkLogEntry entry)
    {
        try
        {
            TalkGroupState? published = null;
            lock (_lock)
            {
                // Salon V2, perroquet ou mode autonome : les lignes résiduelles du daemon
                // ne doivent pas faire apparaître un talkgroup là où il n'y en a pas.
                if (!_applicable)
                    return;

                var next = Interpret(entry.Message, _state);
                if (next is null || next.Equals(_state))
                    return;

                _state = next;
                published = next;
            }

            _logger.LogInformation(
                "Talkgroup courant : {TalkGroup} (origine {Origin})", published.TalkGroup, published.Origin);

            OnTalkGroupChanged?.Invoke(published);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'analyse d'une ligne de log pour le talkgroup");
        }
    }

    /// <summary>
    /// Déduit le nouvel état d'une ligne de log. Retourne <c>null</c> quand la ligne ne
    /// décrit aucun changement.
    /// </summary>
    /// <param name="message">Ligne de log brute émise par SVXLink ou par Logic.tcl.</param>
    /// <param name="current">État courant.</param>
    internal static TalkGroupState? Interpret(string message, TalkGroupState current)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var index = message.IndexOf(EventPrefix, StringComparison.Ordinal);
        if (index >= 0)
            return InterpretEvent(message[(index + EventPrefix.Length)..].Trim(), current);

        // Repli : le message du C++ ne dit que le talkgroup, pas son origine.
        var match = SelectingTgPattern.Match(message);
        if (!match.Success || !TryParse(match.Groups[1].Value, out var selected))
            return null;

        return current.TalkGroup == selected
            ? null
            : current with
            {
                TalkGroup = selected,
                PreviousTalkGroup = current.TalkGroup,
                Origin = TalkGroupActivationOrigin.Unknown
            };
    }

    private static TalkGroupState? InterpretEvent(string payload, TalkGroupState current)
    {
        var parts = payload.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return null;

        return parts[0] switch
        {
            // selected:<nouveau>:<ancien> — le changement de talkgroup lui-même. L'origine
            // arrive dans un événement distinct, que SVXLink émet juste après.
            "selected" when parts.Length >= 3 && TryParse(parts[1], out var tg) =>
                current with
                {
                    TalkGroup = tg,
                    PreviousTalkGroup = TryParse(parts[2], out var previous) ? previous : current.TalkGroup,
                    PendingQsy = null,
                    LastQsyFailed = false
                },

            // activation:<origine>:<nouveau>:<ancien>
            "activation" when parts.Length >= 4 && TryParse(parts[2], out var tg) =>
                current with
                {
                    TalkGroup = tg,
                    PreviousTalkGroup = TryParse(parts[3], out var previous) ? previous : current.PreviousTalkGroup,
                    Origin = ParseOrigin(parts[1])
                },

            // qsy:<nouveau>:<ancien> — le réflecteur a déplacé la conversation.
            "qsy" when parts.Length >= 3 && TryParse(parts[1], out var tg) =>
                current with
                {
                    TalkGroup = tg,
                    PreviousTalkGroup = TryParse(parts[2], out var previous) ? previous : current.PreviousTalkGroup,
                    Origin = TalkGroupActivationOrigin.Qsy,
                    PendingQsy = null,
                    LastQsyFailed = false
                },

            // qsy_pending:<tg> — QSY annoncé, pas encore suivi (QSY_PENDING_TIMEOUT).
            "qsy_pending" when parts.Length >= 2 && TryParse(parts[1], out var tg) =>
                current with { PendingQsy = tg, LastQsyFailed = false },

            // qsy_ignored:<tg> — QSY annoncé puis abandonné, faute d'activité locale.
            "qsy_ignored" => current with { PendingQsy = null },

            "qsy_failed" => current with { PendingQsy = null, LastQsyFailed = true },

            "monitor_add" when parts.Length >= 2 && TryParse(parts[1], out var tg) =>
                current.TemporaryMonitors.Contains(tg)
                    ? null
                    : current with
                    {
                        TemporaryMonitors = current.TemporaryMonitors.Append(tg).Order().ToList()
                    },

            "monitor_remove" when parts.Length >= 2 && TryParse(parts[1], out var tg) =>
                current.TemporaryMonitors.Contains(tg)
                    ? current with
                    {
                        TemporaryMonitors = current.TemporaryMonitors.Where(m => m != tg).ToList()
                    }
                    : null,

            _ => null
        };
    }

    private static TalkGroupActivationOrigin ParseOrigin(string origin) => origin switch
    {
        "local" => TalkGroupActivationOrigin.Local,
        "remote" => TalkGroupActivationOrigin.Remote,
        "priority" => TalkGroupActivationOrigin.Priority,
        "command" => TalkGroupActivationOrigin.Command,
        "default" => TalkGroupActivationOrigin.Default,
        "timeout" => TalkGroupActivationOrigin.Timeout,
        _ => TalkGroupActivationOrigin.Unknown
    };

    private static bool TryParse(string value, out int number) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    public void Dispose()
    {
        if (_disposed)
            return;

        _logService.OnLogReceived -= OnLogReceived;
        _logger.LogInformation("TalkGroupTracker dispose - désabonnement des logs SVXLink");

        _disposed = true;
    }
}
