namespace SvxlinkManagerV2.Application.Features.SystemControl;

/// <summary>
/// Disponibilité des actions d'alimentation (redémarrage / arrêt) sur la plateforme courante.
/// </summary>
/// <param name="IsSupported">Indique si les actions peuvent réellement être déclenchées.</param>
/// <param name="IsSimulated">Indique que les actions sont simulées (mock de développement) et n'auront aucun effet réel.</param>
/// <param name="UnsupportedReason">Raison à afficher à l'utilisateur quand les actions sont indisponibles.</param>
public record SystemControlAvailabilityDto(
    bool IsSupported,
    bool IsSimulated,
    string? UnsupportedReason);
