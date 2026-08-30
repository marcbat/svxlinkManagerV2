using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Suivi de l'état de la liaison au réflecteur, déduit en continu des logs SVXLink.
/// Complète <see cref="ISvxLinkDaemonService"/> qui, lui, ne renseigne que la présence du processus :
/// un daemon actif peut parfaitement n'avoir aucune liaison (clé refusée, hôte injoignable, certificat rejeté).
/// Singleton — l'état n'est pas persisté et repart de <see cref="ReflectorLinkStatus.Inactive"/> à chaque démarrage.
/// </summary>
public interface IReflectorLinkStateService
{
    /// <summary>
    /// État courant de la liaison.
    /// </summary>
    ReflectorLinkState State { get; }

    /// <summary>
    /// Événement déclenché à chaque changement d'état de la liaison.
    /// </summary>
    event Action<ReflectorLinkState>? OnStateChanged;

    /// <summary>
    /// Signale qu'une liaison réflecteur est attendue : le daemon va être (re)démarré
    /// avec une section ReflectorLogic. Repasse l'état à <see cref="ReflectorLinkStatus.Connecting"/>
    /// et oublie l'éventuel échec précédent.
    /// </summary>
    void BeginConnecting();

    /// <summary>
    /// Signale qu'aucune liaison n'est attendue : salon en mode autonome (perroquet)
    /// ou mode simplex. Le suivi des logs est suspendu tant que cet état est actif,
    /// afin qu'aucune ligne résiduelle ne fasse apparaître une liaison en erreur.
    /// </summary>
    void MarkNotApplicable();
}
