namespace SvxlinkManagerV2.Application.Models;

/// <summary>
/// Niveau d'alerte associé à une métrique système.
/// Permet à l'interface de signaler visuellement un dépassement de seuil.
/// </summary>
public enum MetricLevel
{
    /// <summary>Valeur dans la plage nominale.</summary>
    Normal,

    /// <summary>Valeur au-delà du seuil d'avertissement.</summary>
    Warning,

    /// <summary>Valeur au-delà du seuil critique.</summary>
    Critical
}

/// <summary>
/// Charge processeur telle que rapportée par <c>/proc/loadavg</c>.
/// </summary>
/// <param name="Load1">Charge moyenne sur 1 minute.</param>
/// <param name="Load5">Charge moyenne sur 5 minutes.</param>
/// <param name="Load15">Charge moyenne sur 15 minutes.</param>
/// <param name="CoreCount">Nombre de cœurs logiques de la machine.</param>
public record CpuLoadMetrics(double Load1, double Load5, double Load15, int CoreCount)
{
    /// <summary>
    /// Charge sur 1 minute ramenée au nombre de cœurs, exprimée en pourcentage.
    /// Peut dépasser 100 % en cas de surcharge.
    /// </summary>
    public double LoadPercent => CoreCount <= 0 ? 0 : Load1 / CoreCount * 100d;
}

/// <summary>
/// Occupation mémoire telle que rapportée par <c>/proc/meminfo</c>.
/// </summary>
/// <param name="TotalBytes">Mémoire physique totale en octets.</param>
/// <param name="AvailableBytes">Mémoire disponible (allouable sans swap) en octets.</param>
public record MemoryMetrics(long TotalBytes, long AvailableBytes)
{
    /// <summary>Mémoire utilisée en octets.</summary>
    public long UsedBytes => Math.Max(0, TotalBytes - AvailableBytes);

    /// <summary>Pourcentage de mémoire utilisée.</summary>
    public double UsedPercent => TotalBytes <= 0 ? 0 : (double)UsedBytes / TotalBytes * 100d;
}

/// <summary>
/// Occupation d'une partition disque.
/// </summary>
/// <param name="MountPath">Chemin interrogé (point de montage ou répertoire).</param>
/// <param name="TotalBytes">Taille totale de la partition en octets.</param>
/// <param name="AvailableBytes">Espace libre disponible en octets.</param>
public record DiskMetrics(string MountPath, long TotalBytes, long AvailableBytes)
{
    /// <summary>Espace disque utilisé en octets.</summary>
    public long UsedBytes => Math.Max(0, TotalBytes - AvailableBytes);

    /// <summary>Pourcentage d'espace disque utilisé.</summary>
    public double UsedPercent => TotalBytes <= 0 ? 0 : (double)UsedBytes / TotalBytes * 100d;
}

/// <summary>
/// Durées de fonctionnement de la machine et du processus applicatif.
/// </summary>
/// <param name="Machine">Uptime de la machine.</param>
/// <param name="Process">Uptime du processus SvxlinkManager.</param>
public record UptimeMetrics(TimeSpan Machine, TimeSpan Process);
