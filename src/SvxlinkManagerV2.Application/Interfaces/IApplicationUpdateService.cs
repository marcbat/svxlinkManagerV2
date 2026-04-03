using LanguageExt;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de consultation des mises à jour applicatives publiées sur le canal configuré.
/// </summary>
public interface IApplicationUpdateService
{
    /// <summary>
    /// Récupère le statut courant de mise à jour en interrogeant la source distante.
    /// </summary>
    /// <param name="channel">Canal à consulter. Si null, le canal configuré est utilisé.</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Statut courant de mise à jour</returns>
    Task<Validation<Error, ApplicationUpdateStatusDto>> GetStatusAsync(
        ApplicationUpdateChannel? channel = null,
        CancellationToken cancellationToken = default);
}