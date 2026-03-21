// Cet événement a été supprimé : l'état actif est désormais géré par IActiveSessionTracker (runtime uniquement).
// Fichier conservé uniquement pour les stubs obsolètes des tests en attente de mise à jour.
namespace SvxlinkManagerV2.Domain.Aggregates.Salon.Events;

[Obsolete("SalonDeactivated est supprimé. Utiliser IActiveSessionTracker.")]
public record SalonDeactivated
{
    public Guid Id { get; init; }
    public SalonDeactivated(Guid id) { Id = id; }
}
