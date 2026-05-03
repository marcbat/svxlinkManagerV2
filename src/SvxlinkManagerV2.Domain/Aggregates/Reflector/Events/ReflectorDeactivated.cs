// Cet événement a été supprimé : l'état actif est désormais géré par IActiveSessionTracker (runtime uniquement).
// Fichier conservé uniquement pour les stubs obsolètes des tests.
namespace SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;

[Obsolete("ReflectorDeactivated est supprimé. Utiliser IActiveSessionTracker.")]
public record ReflectorDeactivated
{
    public Guid Id { get; init; }
    public ReflectorDeactivated(Guid id) { Id = id; }
}
