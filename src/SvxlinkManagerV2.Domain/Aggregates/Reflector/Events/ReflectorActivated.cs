// Cet événement a été supprimé : l'état actif est désormais géré par IActiveSessionTracker (runtime uniquement).
// Fichier conservé uniquement pour les stubs obsolètes des tests.
namespace SvxlinkManagerV2.Domain.Aggregates.Reflector.Events;

[Obsolete("ReflectorActivated est supprimé. Utiliser IActiveSessionTracker.")]
public record ReflectorActivated
{
    public Guid Id { get; init; }
    public ReflectorActivated(Guid id) { Id = id; }
}
