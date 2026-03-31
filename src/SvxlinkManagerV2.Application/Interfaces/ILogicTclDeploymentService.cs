using LanguageExt;
using LanguageExt.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de déploiement du fichier Logic.tcl vers le répertoire d'événements SVXLink local.
/// Ce fichier surcharge le handler "startup" de SVXLink pour jouer l'annonce du salon
/// une seule fois au démarrage du daemon (one-shot).
/// </summary>
public interface ILogicTclDeploymentService
{
    /// <summary>
    /// Déploie le fichier Logic.tcl vers /usr/share/svxlink/events.d/local/Logic.tcl.
    /// Crée le répertoire cible s'il n'existe pas.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    Task<Validation<Error, Unit>> DeployAsync(CancellationToken cancellationToken = default);
}
