using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service d'interaction avec le daemon SVXLink (systemctl).
/// Permet de démarrer, arrêter, redémarrer et vérifier l'état du daemon.
/// </summary>
public interface ISvxLinkDaemonService
{
    /// <summary>
    /// Redémarre le daemon SVXLink via systemctl.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    Task<Validation<Error, Unit>> RestartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Arrête le daemon SVXLink.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    Task<Validation<Error, Unit>> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Envoie une commande DTMF au daemon SVXLink via le pseudo-terminal de contrôle.
    /// </summary>
    /// <param name="sequence">Séquence DTMF à envoyer (ex: "2#" pour activer le ModuleParrot)</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    Task<Validation<Error, Unit>> SendDtmfCommandAsync(string sequence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vérifie si le daemon SVXLink est actuellement en cours d'exécution.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant si le daemon est actif</returns>
    Task<Validation<Error, bool>> IsRunningAsync(CancellationToken cancellationToken = default);
}
