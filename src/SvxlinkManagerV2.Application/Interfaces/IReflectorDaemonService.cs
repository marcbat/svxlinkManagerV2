using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service d'interaction avec le daemon svxreflector.
/// Permet de démarrer, arrêter et vérifier l'état du daemon reflector.
/// </summary>
public interface IReflectorDaemonService
{
    /// <summary>
    /// Redémarre le daemon svxreflector.
    /// Arrête le processus s'il tourne, puis relit la configuration et redémarre.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    Task<Validation<Error, Unit>> RestartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Arrête le daemon svxreflector.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    Task<Validation<Error, Unit>> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Vérifie si le daemon svxreflector est actuellement en cours d'exécution.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant si le daemon est actif</returns>
    Task<Validation<Error, bool>> IsRunningAsync(CancellationToken cancellationToken = default);
}
