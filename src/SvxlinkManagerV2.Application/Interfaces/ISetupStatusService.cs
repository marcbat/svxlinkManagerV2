namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de détection du premier lancement de l'application.
/// Permet de savoir si le wizard de configuration initiale doit être affiché.
/// </summary>
public interface ISetupStatusService
{
    /// <summary>
    /// Détermine si la configuration initiale est requise (base vide, aucun salon).
    /// Le résultat est mis en cache jusqu'à l'invalidation explicite via <see cref="InvalidateCache"/>.
    /// </summary>
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalide le cache du statut de configuration (à appeler après la fin du wizard).
    /// </summary>
    void InvalidateCache();
}
