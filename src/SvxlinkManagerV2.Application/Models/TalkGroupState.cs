namespace SvxlinkManagerV2.Application.Models;

/// <summary>
/// Ce qui a provoqué la sélection du talkgroup courant.
/// </summary>
/// <remarks>
/// Reprise des procédures d'événement de <c>ReflectorLogic.tcl</c> : SVXLink distingue
/// lui-même ces origines, et elles ne se déduisent pas des lignes de log.
/// </remarks>
public enum TalkGroupActivationOrigin
{
    /// <summary>Origine inconnue, ou aucun talkgroup sélectionné.</summary>
    Unknown,

    /// <summary>Activité locale : l'opérateur a émis.</summary>
    Local,

    /// <summary>Activité d'un nœud distant sur ce talkgroup.</summary>
    Remote,

    /// <summary>Activité sur un talkgroup surveillé plus prioritaire que le courant.</summary>
    Priority,

    /// <summary>Commande talkgroup composée en DTMF ou injectée par l'application.</summary>
    Command,

    /// <summary>Retour au <c>DEFAULT_TG</c> du salon.</summary>
    Default,

    /// <summary>QSY : le talkgroup a été déplacé par le réflecteur.</summary>
    Qsy,

    /// <summary><c>TG_SELECT_TIMEOUT</c> écoulé : SVXLink a relâché le talkgroup.</summary>
    Timeout
}

/// <summary>
/// État courant des talkgroups du nœud (protocole V3).
/// </summary>
/// <param name="TalkGroup">
/// Talkgroup courant. <c>null</c> quand la notion n'a pas de sens — salon V2, perroquet,
/// mode autonome. <c>0</c> signifie « aucun talkgroup », ce qui n'est pas la même chose.
/// </param>
/// <param name="PreviousTalkGroup">Talkgroup quitté lors de la dernière sélection.</param>
/// <param name="Origin">Ce qui a provoqué la sélection courante.</param>
/// <param name="TemporaryMonitors">
/// Talkgroups surveillés temporairement (<c>TMP_MONITOR_TIMEOUT</c>), en plus de ceux de
/// <c>MONITOR_TGS</c> qui sont, eux, une donnée de configuration du salon.
/// </param>
/// <param name="PendingQsy">Talkgroup d'un QSY annoncé mais pas encore suivi.</param>
/// <param name="LastQsyFailed">Le dernier QSY demandé a échoué.</param>
public record TalkGroupState(
    int? TalkGroup,
    int? PreviousTalkGroup = null,
    TalkGroupActivationOrigin Origin = TalkGroupActivationOrigin.Unknown,
    IReadOnlyList<int>? TemporaryMonitors = null,
    int? PendingQsy = null,
    bool LastQsyFailed = false)
{
    /// <summary>Le salon actif n'a pas de talkgroup.</summary>
    /// <remarks>
    /// Le paramètre est nommé : <c>new(null)</c> se lierait au constructeur de copie généré
    /// par le record, qui déréférencerait ce null.
    /// </remarks>
    public static readonly TalkGroupState NotApplicable = new(TalkGroup: null);

    /// <summary>Talkgroups surveillés temporairement, jamais <c>null</c>.</summary>
    public IReadOnlyList<int> TemporaryMonitors { get; init; } = TemporaryMonitors ?? [];

    /// <summary>La notion de talkgroup s'applique au salon actif.</summary>
    public bool IsApplicable => TalkGroup.HasValue;

    /// <summary>
    /// Égalité structurelle, y compris pour <see cref="TemporaryMonitors"/>.
    /// </summary>
    /// <remarks>
    /// L'égalité générée par le record comparerait la liste par référence : deux états de
    /// même contenu paraîtraient différents, et chaque ligne de log notifierait l'interface
    /// pour rien.
    /// </remarks>
    public virtual bool Equals(TalkGroupState? other) =>
        other is not null
        && TalkGroup == other.TalkGroup
        && PreviousTalkGroup == other.PreviousTalkGroup
        && Origin == other.Origin
        && PendingQsy == other.PendingQsy
        && LastQsyFailed == other.LastQsyFailed
        && TemporaryMonitors.SequenceEqual(other.TemporaryMonitors);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(TalkGroup, PreviousTalkGroup, Origin, PendingQsy, LastQsyFailed, TemporaryMonitors.Count);
}
