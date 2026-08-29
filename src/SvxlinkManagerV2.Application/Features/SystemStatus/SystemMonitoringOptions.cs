namespace SvxlinkManagerV2.Application.Features.SystemStatus;

/// <summary>
/// Options de supervision système : chemins interrogés et seuils d'alerte.
/// Configurables via la section <c>SystemMonitoring</c> des appsettings.
/// </summary>
public class SystemMonitoringOptions
{
    /// <summary>Nom de la section de configuration.</summary>
    public const string SectionName = "SystemMonitoring";

    /// <summary>Point de montage de la partition système.</summary>
    public string SystemMountPath { get; set; } = "/";

    /// <summary>Répertoire de données de l'application (base SQLite, mises à jour).</summary>
    public string DataPath { get; set; } = "data";

    /// <summary>Seuil d'avertissement de température CPU en degrés Celsius.</summary>
    public double CpuTemperatureWarningCelsius { get; set; } = 65;

    /// <summary>Seuil critique de température CPU en degrés Celsius.</summary>
    public double CpuTemperatureCriticalCelsius { get; set; } = 75;

    /// <summary>Seuil d'avertissement d'occupation disque en pourcentage.</summary>
    public double DiskUsageWarningPercent { get; set; } = 80;

    /// <summary>Seuil critique d'occupation disque en pourcentage.</summary>
    public double DiskUsageCriticalPercent { get; set; } = 90;

    /// <summary>Seuil d'avertissement d'occupation mémoire en pourcentage.</summary>
    public double MemoryUsageWarningPercent { get; set; } = 85;

    /// <summary>Seuil critique d'occupation mémoire en pourcentage.</summary>
    public double MemoryUsageCriticalPercent { get; set; } = 95;

    /// <summary>Seuil d'avertissement de charge CPU en pourcentage d'un cœur.</summary>
    public double CpuLoadWarningPercent { get; set; } = 80;

    /// <summary>Seuil critique de charge CPU en pourcentage d'un cœur.</summary>
    public double CpuLoadCriticalPercent { get; set; } = 100;

    /// <summary>Seuil en-dessous duquel la qualité du lien WiFi est signalée comme faible.</summary>
    public int WifiSignalWarningPercent { get; set; } = 40;
}
