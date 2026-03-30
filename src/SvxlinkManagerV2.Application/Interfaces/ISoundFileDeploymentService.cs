using LanguageExt;
using LanguageExt.Common;
using SvxlinkManagerV2.Domain.Aggregates.Sound;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de déploiement du fichier son WAV sur le filesystem du serveur.
/// Utilisé lors de l'activation d'un Salon pour préparer l'annonce SVXLink.
/// </summary>
public interface ISoundFileDeploymentService
{
    /// <summary>
    /// Déploie le fichier WAV d'un Sound sur le filesystem de manière atomique.
    /// </summary>
    /// <param name="sound">Aggregate Sound contenant le contenu WAV</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Chemin absolu du fichier déployé en cas de succès</returns>
    Task<Validation<Error, string>> DeployAsync(
        SoundAggregate sound,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime le fichier son déployé s'il existe.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    Task<Validation<Error, Unit>> CleanupAsync(
        CancellationToken cancellationToken = default);
}
