using System.Globalization;
using System.Text.RegularExpressions;

namespace SvxlinkManagerV2.Domain.Aggregates.Salon;

/// <summary>
/// Décrit une commande DTMF talkgroup exposée aux opérateurs.
/// </summary>
/// <param name="Pattern">Séquence à composer, <c>&lt;tg&gt;</c> désignant un numéro de talkgroup.</param>
/// <param name="Description">Description en français de l'action déclenchée.</param>
public record DtmfTalkGroupCommand(string Pattern, string Description);

/// <summary>
/// Catalogue des commandes talkgroup du protocole V3, adressées à <c>ReflectorLogic</c>
/// à travers le lien <c>[LinkToReflector]</c>.
///
/// Ces commandes ne sont pas traitées par l'application : SVXLink les route lui-même, à
/// condition que <c>CONNECT_LOGICS</c> déclare un préfixe de commande sur la logique simplex
/// (<c>SimplexLogic:35</c>). <c>LinkManager::addLogic</c> conditionne en effet la création de
/// l'objet de commande à <c>atoi(cmd) &gt; 0</c> : sans préfixe, rien n'atteint jamais
/// <c>ReflectorLogic::remoteCmdReceived</c>.
///
/// Source unique de vérité : utilisée par la génération de <c>svxlink.conf</c>, par la page
/// d'aide, et par la sélection de talkgroup à chaud (injection dans le PTY DTMF).
///
/// Un salon en protocole V2 ne connaît pas les talkgroups : aucun préfixe n'est déclaré et
/// aucune de ces commandes n'existe.
/// </summary>
public static class DtmfTalkGroupCommands
{
    /// <summary>Préfixe de commande, repris de <see cref="DtmfCodeRanges.TalkGroupCommandPrefix"/>.</summary>
    public const string Prefix = DtmfCodeRanges.TalkGroupCommandPrefix;

    /// <summary>
    /// Sous-commandes reconnues par <c>ReflectorLogic::remoteCmdReceived</c> : <c>*</c> (état),
    /// puis 1 à 4 éventuellement suivis d'un numéro de talkgroup.
    ///
    /// La sous-commande vide (<c>35#</c> seul) est volontairement exclue : <c>LinkManager::cmdReceived</c>
    /// l'interpréterait comme une <b>désactivation du lien</b>, ce qui couperait l'audio entre la
    /// radio et le réflecteur sur une simple faute de frappe.
    /// </summary>
    private static readonly Regex CommandPattern =
        new("^" + Prefix + @"(\*|[1-4][0-9]*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Annonce vocale du talkgroup courant et de l'état de la liaison.
    /// La séquence composée est <c>35*#</c> : le <c>*</c> fait partie de la commande, et
    /// c'est bien le <c>#</c> qui la termine — vérifié sur la stack Docker, <c>35*</c> seul
    /// laisse SVXLink en attente de la suite. Le <c>#</c> est ajouté par
    /// <c>IDtmfPtyWriter</c>, cette valeur ne le porte donc pas.
    /// </summary>
    public static string Status => $"{Prefix}*";

    /// <summary>Retour au talkgroup précédemment sélectionné.</summary>
    public static string PreviousTalkGroup => $"{Prefix}1";

    /// <summary>Demande de QSY vers un talkgroup tiré au hasard (<c>RANDOM_QSY_RANGE</c>).</summary>
    public static string RandomQsy => $"{Prefix}2";

    /// <summary>Suivi du dernier QSY annoncé.</summary>
    public static string FollowLastQsy => $"{Prefix}3";

    /// <summary>Sélection du talkgroup <paramref name="talkGroup"/>.</summary>
    public static string SelectTalkGroup(int talkGroup)
        => $"{Prefix}1{talkGroup.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Demande de QSY vers le talkgroup <paramref name="talkGroup"/>.</summary>
    public static string RequestQsy(int talkGroup)
        => $"{Prefix}2{talkGroup.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Surveillance temporaire du talkgroup <paramref name="talkGroup"/> (<c>TMP_MONITOR_TIMEOUT</c>).</summary>
    public static string TemporaryMonitor(int talkGroup)
        => $"{Prefix}4{talkGroup.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Indique si la séquence composée est une commande talkgroup, et doit donc être laissée
    /// à SVXLink plutôt que traitée comme un code salon.
    /// </summary>
    /// <param name="rawCommand">Séquence DTMF telle que reçue, sans le <c>#</c> terminal.</param>
    public static bool IsTalkGroupCommand(string? rawCommand)
        => !string.IsNullOrWhiteSpace(rawCommand) && CommandPattern.IsMatch(rawCommand.Trim());

    /// <summary>
    /// Liste des commandes talkgroup exposées aux opérateurs, dans l'ordre d'usage.
    /// </summary>
    public static IReadOnlyList<DtmfTalkGroupCommand> All { get; } =
    [
        new($"{Prefix}*#", "Annoncer le talkgroup courant et l'état de la liaison"),
        new($"{Prefix}1<tg>#", "Sélectionner le talkgroup <tg>"),
        new($"{Prefix}1#", "Revenir au talkgroup précédent"),
        new($"{Prefix}2<tg>#", "Demander un QSY vers le talkgroup <tg>"),
        new($"{Prefix}2#", "Demander un QSY vers un talkgroup tiré au hasard"),
        new($"{Prefix}3#", "Suivre le dernier QSY annoncé"),
        new($"{Prefix}4<tg>#", "Surveiller temporairement le talkgroup <tg>")
    ];
}
