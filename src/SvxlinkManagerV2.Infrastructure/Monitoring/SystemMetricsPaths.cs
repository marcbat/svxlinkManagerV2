namespace SvxlinkManagerV2.Infrastructure.Monitoring;

/// <summary>
/// Chemins des pseudo-fichiers Linux interrogés pour les métriques système.
/// Injectables afin de rendre <see cref="LinuxSystemMetricsService"/> testable
/// sur une plateforme dépourvue de <c>/proc</c> et <c>/sys</c>.
/// </summary>
public class SystemMetricsPaths
{
    /// <summary>Chemin par défaut du capteur de température CPU.</summary>
    public const string DefaultThermalZone = "/sys/class/thermal/thermal_zone0/temp";

    /// <summary>Chemin par défaut des charges moyennes du noyau.</summary>
    public const string DefaultLoadAvg = "/proc/loadavg";

    /// <summary>Chemin par défaut des statistiques mémoire du noyau.</summary>
    public const string DefaultMemInfo = "/proc/meminfo";

    /// <summary>Chemin par défaut de l'uptime machine.</summary>
    public const string DefaultUptime = "/proc/uptime";

    /// <summary>Capteur de température CPU.</summary>
    public string ThermalZone { get; init; } = DefaultThermalZone;

    /// <summary>Charges moyennes du noyau.</summary>
    public string LoadAvg { get; init; } = DefaultLoadAvg;

    /// <summary>Statistiques mémoire du noyau.</summary>
    public string MemInfo { get; init; } = DefaultMemInfo;

    /// <summary>Uptime machine.</summary>
    public string Uptime { get; init; } = DefaultUptime;
}
