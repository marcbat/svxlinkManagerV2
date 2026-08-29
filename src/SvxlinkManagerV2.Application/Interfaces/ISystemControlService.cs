using LanguageExt;
using SvxlinkManagerV2.Application.Features.SystemControl;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de contrôle de l'alimentation de la machine hôte.
/// Arrête proprement les daemons SVXLink et svxreflector avant de déclencher l'appel système.
/// </summary>
public interface ISystemControlService
{
    /// <summary>
    /// Indique si la plateforme courante supporte le redémarrage et l'arrêt de la machine.
    /// </summary>
    /// <returns>Disponibilité et, le cas échéant, la raison de l'indisponibilité</returns>
    SystemControlAvailabilityDto GetAvailability();

    /// <summary>
    /// Redémarre la machine après avoir arrêté les daemons.
    /// L'appel système est planifié en arrière-plan : la méthode retourne avant que la machine ne redémarre.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant que le redémarrage a bien été planifié</returns>
    Task<Validation<Error, Unit>> RebootAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Arrête la machine après avoir arrêté les daemons.
    /// L'appel système est planifié en arrière-plan : la méthode retourne avant que la machine ne s'éteigne.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant que l'arrêt a bien été planifié</returns>
    Task<Validation<Error, Unit>> ShutdownAsync(CancellationToken cancellationToken = default);
}
