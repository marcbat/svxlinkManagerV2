namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Suivi en mémoire de l'état d'activité courant (runtime).
/// IsActive n'est PAS persisté — ce singleton est réinitialisé à chaque démarrage de l'application.
/// </summary>
public interface IActiveSessionTracker
{
    /// <summary>
    /// Identifiant du Salon actuellement actif, ou null si aucun.
    /// </summary>
    Guid? ActiveSalonId { get; }

    /// <summary>
    /// Identifiant du Reflector actuellement actif, ou null si aucun.
    /// </summary>
    Guid? ActiveReflectorId { get; }

    /// <summary>
    /// Définit le Salon actif. Passer null pour indiquer qu'aucun salon n'est actif.
    /// </summary>
    void SetActiveSalon(Guid? id);

    /// <summary>
    /// Définit le Reflector actif. Passer null pour indiquer qu'aucun reflector n'est actif.
    /// </summary>
    void SetActiveReflector(Guid? id);

    /// <summary>
    /// Indique si le Salon identifié est actuellement actif.
    /// </summary>
    bool IsSalonActive(Guid id);

    /// <summary>
    /// Indique si le Reflector identifié est actuellement actif.
    /// </summary>
    bool IsReflectorActive(Guid id);
}
