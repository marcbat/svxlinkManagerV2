using LanguageExt;
using SvxlinkManagerV2.Application.Features.ApplicationUpdate;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Orchestrateur applicatif du workflow de mise à jour: consultation, téléchargement et demande d'installation.
/// </summary>
public interface IApplicationUpdateWorkflowService
{
    /// <summary>
    /// Récupère le statut complet du workflow de mise à jour pour le canal demandé.
    /// </summary>
    Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> GetStatusAsync(
        ApplicationUpdateChannel? channel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Télécharge localement le paquet .deb de la dernière release disponible sur le canal demandé.
    /// </summary>
    Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> DownloadLatestAsync(
        ApplicationUpdateChannel? channel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Déclenche la demande d'installation via la commande/helper configuré.
    /// </summary>
    Task<Validation<Error, ApplicationUpdateWorkflowStatusDto>> RequestInstallAsync(
        CancellationToken cancellationToken = default);
}