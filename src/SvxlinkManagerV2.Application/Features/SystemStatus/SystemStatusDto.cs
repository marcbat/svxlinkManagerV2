using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Wifi;

namespace SvxlinkManagerV2.Application.Features.SystemStatus;

/// <summary>
/// Une métrique du tableau de supervision : soit une valeur avec son niveau d'alerte,
/// soit la raison de son indisponibilité sur la plateforme courante.
/// </summary>
/// <typeparam name="T">Type de la valeur mesurée.</typeparam>
/// <param name="Value">Valeur mesurée, null si indisponible.</param>
/// <param name="Level">Niveau d'alerte calculé à partir des seuils configurés.</param>
/// <param name="UnavailableReason">Message expliquant l'indisponibilité, null si disponible.</param>
public record SystemMetric<T>(T? Value, MetricLevel Level, string? UnavailableReason)
    where T : class
{
    /// <summary>Indique que la métrique a pu être lue.</summary>
    public bool IsAvailable => Value is not null;

    /// <summary>Crée une métrique disponible.</summary>
    public static SystemMetric<T> Available(T value, MetricLevel level = MetricLevel.Normal)
        => new(value, level, null);

    /// <summary>Crée une métrique indisponible.</summary>
    public static SystemMetric<T> Unavailable(string reason)
        => new(null, MetricLevel.Normal, reason);
}

/// <summary>
/// Métrique numérique : même contrat que <see cref="SystemMetric{T}"/> pour les types valeur.
/// </summary>
/// <param name="Value">Valeur mesurée, null si indisponible.</param>
/// <param name="Level">Niveau d'alerte calculé à partir des seuils configurés.</param>
/// <param name="UnavailableReason">Message expliquant l'indisponibilité, null si disponible.</param>
public record SystemValueMetric(double? Value, MetricLevel Level, string? UnavailableReason)
{
    /// <summary>Indique que la métrique a pu être lue.</summary>
    public bool IsAvailable => Value.HasValue;

    /// <summary>Crée une métrique disponible.</summary>
    public static SystemValueMetric Available(double value, MetricLevel level = MetricLevel.Normal)
        => new(value, level, null);

    /// <summary>Crée une métrique indisponible.</summary>
    public static SystemValueMetric Unavailable(string reason)
        => new(null, MetricLevel.Normal, reason);
}

/// <summary>
/// Occupation d'une partition identifiée par un libellé fonctionnel.
/// </summary>
/// <param name="Label">Libellé affiché (ex : « Partition système »).</param>
/// <param name="Path">Chemin interrogé.</param>
/// <param name="Metric">Mesure d'occupation ou raison d'indisponibilité.</param>
public record DiskStatusDto(string Label, string Path, SystemMetric<DiskMetrics> Metric);

/// <summary>
/// État d'une installation SVXLink pilotée par l'application.
/// </summary>
/// <param name="Name">Nom de l'installation (ex : « SVXLink legacy »).</param>
/// <param name="Version">Version upstream de SVXLink.</param>
/// <param name="Protocol">Protocole réflecteur pris en charge.</param>
/// <param name="BinaryPath">Chemin du binaire svxlink.</param>
/// <param name="IsInstalled">Indique que le binaire est présent sur la machine.</param>
public record SvxLinkInstallationDto(
    string Name,
    string Version,
    string Protocol,
    string BinaryPath,
    bool IsInstalled);

/// <summary>
/// Instantané de l'état de la machine, agrégé pour la page de supervision.
/// Chaque métrique est indépendante : l'indisponibilité de l'une n'invalide pas les autres.
/// </summary>
/// <param name="CollectedAt">Horodatage de la collecte.</param>
/// <param name="CpuTemperatureCelsius">Température du processeur en degrés Celsius.</param>
/// <param name="CpuLoad">Charge processeur moyenne.</param>
/// <param name="Memory">Occupation de la mémoire physique.</param>
/// <param name="Disks">Occupation des partitions supervisées.</param>
/// <param name="Uptime">Uptime machine et processus.</param>
/// <param name="Network">État du lien réseau actif.</param>
/// <param name="ApplicationVersion">Version de l'application.</param>
/// <param name="SvxLinkInstallations">Installations SVXLink détectées.</param>
public record SystemStatusDto(
    DateTimeOffset CollectedAt,
    SystemValueMetric CpuTemperatureCelsius,
    SystemMetric<CpuLoadMetrics> CpuLoad,
    SystemMetric<MemoryMetrics> Memory,
    IReadOnlyList<DiskStatusDto> Disks,
    SystemMetric<UptimeMetrics> Uptime,
    SystemMetric<WifiLink> Network,
    string ApplicationVersion,
    IReadOnlyList<SvxLinkInstallationDto> SvxLinkInstallations);
