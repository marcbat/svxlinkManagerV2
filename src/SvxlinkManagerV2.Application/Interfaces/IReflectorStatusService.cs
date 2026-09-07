using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Statut du réflecteur local, obtenu de son serveur HTTP intégré (<c>HTTP_SRV_PORT</c>).
/// </summary>
/// <remarks>
/// C'est l'interface prévue par l'amont pour la supervision — l'outil officiel
/// <c>svxreflector-status</c> ne fait rien d'autre que la lire. Elle donne ce que le parsing
/// des logs ne peut pas donner : le talkgroup de chaque nœud, ses talkgroups surveillés et
/// sa version de protocole.
/// </remarks>
public interface IReflectorStatusService
{
    /// <summary>Dernier instantané connu. Jamais <c>null</c>.</summary>
    ReflectorStatusSnapshot Current { get; }

    /// <summary>Émis à chaque changement d'instantané.</summary>
    event Action<ReflectorStatusSnapshot>? OnStatusChanged;
}
