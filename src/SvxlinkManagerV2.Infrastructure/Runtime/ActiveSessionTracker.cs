using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.Runtime;

/// <summary>
/// Implémentation thread-safe du tracker d'état actif (runtime, singleton).
/// Réinitialisé à chaque démarrage — aucune persistance.
/// </summary>
public class ActiveSessionTracker : IActiveSessionTracker
{
    private readonly object _lock = new();
    private Guid? _activeSalonId;
    private Guid? _activeReflectorId;

    public Guid? ActiveSalonId
    {
        get { lock (_lock) return _activeSalonId; }
    }

    public Guid? ActiveReflectorId
    {
        get { lock (_lock) return _activeReflectorId; }
    }

    public void SetActiveSalon(Guid? id)
    {
        lock (_lock) _activeSalonId = id;
    }

    public void SetActiveReflector(Guid? id)
    {
        lock (_lock) _activeReflectorId = id;
    }

    public bool IsSalonActive(Guid id)
    {
        lock (_lock) return _activeSalonId == id;
    }

    public bool IsReflectorActive(Guid id)
    {
        lock (_lock) return _activeReflectorId == id;
    }
}
