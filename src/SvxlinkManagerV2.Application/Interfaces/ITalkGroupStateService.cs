namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Suivi du talkgroup courant du nœud (protocole V3).
///
/// Le talkgroup n'est pas un état persisté : il vit dans le daemon SVXLink, et peut changer
/// sans que l'application en soit l'origine — commande DTMF composée sur la radio, QSY décidé
/// par le réflecteur, bascule sur un talkgroup prioritaire. La source de vérité est donc le
/// flux de logs, pas la commande qui a demandé le changement.
///
/// <see cref="Current"/> vaut <c>null</c> quand la notion n'a pas de sens : salon en protocole
/// V2, salon perroquet ou mode autonome.
/// </summary>
public interface ITalkGroupStateService
{
    /// <summary>Talkgroup courant, ou <c>null</c> si le salon actif n'a pas de talkgroups.</summary>
    int? Current { get; }

    /// <summary>Émis à chaque changement effectif du talkgroup courant.</summary>
    event Action<int?>? OnTalkGroupChanged;

    /// <summary>
    /// Repositionne le suivi sur le talkgroup par défaut du salon activé.
    /// Appelé à l'activation d'un salon V3, avant le redémarrage du daemon : une sélection
    /// faite à chaud sur le salon précédent ne doit pas survivre à l'activation.
    /// </summary>
    /// <param name="talkGroup">Valeur de <c>DEFAULT_TG</c> du salon.</param>
    void ApplyDefault(int talkGroup);

    /// <summary>
    /// Signale que le salon actif n'a pas de talkgroup (V2, perroquet, mode autonome).
    /// Les lignes de log résiduelles du daemon sont alors ignorées.
    /// </summary>
    void MarkNotApplicable();
}
