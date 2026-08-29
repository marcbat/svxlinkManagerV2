using LanguageExt;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Lecture des métriques système de la machine hôte (température, charge, mémoire, disque, uptime).
/// Chaque métrique est indépendante : une métrique indisponible sur la plateforme courante
/// retourne un échec sans empêcher la lecture des autres.
/// </summary>
public interface ISystemMetricsService
{
    /// <summary>
    /// Température du processeur en degrés Celsius.
    /// </summary>
    Task<Validation<Error, double>> GetCpuTemperatureCelsiusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge processeur moyenne (1, 5 et 15 minutes) et nombre de cœurs.
    /// </summary>
    Task<Validation<Error, CpuLoadMetrics>> GetCpuLoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Occupation de la mémoire physique.
    /// </summary>
    Task<Validation<Error, MemoryMetrics>> GetMemoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Occupation de la partition contenant le chemin fourni.
    /// </summary>
    /// <param name="path">Point de montage ou répertoire à interroger.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, DiskMetrics>> GetDiskAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Durées de fonctionnement de la machine et du processus applicatif.
    /// </summary>
    Task<Validation<Error, UptimeMetrics>> GetUptimeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Version de l'application en cours d'exécution.
    /// </summary>
    string GetApplicationVersion();
}
