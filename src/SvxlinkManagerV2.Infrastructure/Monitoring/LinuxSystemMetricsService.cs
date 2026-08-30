using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using LanguageExt;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.Monitoring;

/// <summary>
/// Lecture des métriques système depuis les pseudo-systèmes de fichiers Linux
/// (<c>/proc</c>, <c>/sys</c>) et l'API disque de .NET.
///
/// Aucune métrique ne lève d'exception : toute source absente ou illisible
/// est convertie en erreur métier, laissant l'appelant afficher l'information
/// comme indisponible.
/// </summary>
public class LinuxSystemMetricsService : ISystemMetricsService
{
    private readonly ILogger<LinuxSystemMetricsService> _logger;
    private readonly SystemMetricsPaths _paths;

    public LinuxSystemMetricsService(
        ILogger<LinuxSystemMetricsService> logger,
        SystemMetricsPaths? paths = null)
    {
        _logger = logger;
        _paths = paths ?? new SystemMetricsPaths();
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, double>> GetCpuTemperatureCelsiusAsync(
        CancellationToken cancellationToken = default)
    {
        var content = await ReadFileAsync(_paths.ThermalZone, "SYSTEM_TEMPERATURE_UNAVAILABLE", cancellationToken);

        return content.Bind(raw =>
        {
            if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliDegrees))
            {
                return Fail<double>(
                    "SYSTEM_TEMPERATURE_INVALID",
                    $"Valeur de température invalide : « {raw.Trim()} »");
            }

            // Le noyau expose la température en milli-degrés Celsius.
            return Validation<Error, double>.Success(milliDegrees / 1000d);
        });
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, CpuLoadMetrics>> GetCpuLoadAsync(
        CancellationToken cancellationToken = default)
    {
        var content = await ReadFileAsync(_paths.LoadAvg, "SYSTEM_CPU_LOAD_UNAVAILABLE", cancellationToken);

        return content.Bind(raw =>
        {
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length < 3
                || !TryParseDouble(parts[0], out var load1)
                || !TryParseDouble(parts[1], out var load5)
                || !TryParseDouble(parts[2], out var load15))
            {
                return Fail<CpuLoadMetrics>(
                    "SYSTEM_CPU_LOAD_INVALID",
                    $"Contenu de charge processeur invalide : « {raw.Trim()} »");
            }

            return Validation<Error, CpuLoadMetrics>.Success(
                new CpuLoadMetrics(load1, load5, load15, Environment.ProcessorCount));
        });
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, MemoryMetrics>> GetMemoryAsync(
        CancellationToken cancellationToken = default)
    {
        var content = await ReadFileAsync(_paths.MemInfo, "SYSTEM_MEMORY_UNAVAILABLE", cancellationToken);

        return content.Bind(raw =>
        {
            var values = ParseMemInfo(raw);

            if (!values.TryGetValue("MemTotal", out var totalKb) || totalKb <= 0)
            {
                return Fail<MemoryMetrics>(
                    "SYSTEM_MEMORY_INVALID",
                    "Ligne MemTotal absente ou invalide dans les statistiques mémoire");
            }

            // MemAvailable n'existe pas sur les noyaux antérieurs à 3.14 : on retombe
            // sur l'approximation historique MemFree + Buffers + Cached.
            if (!values.TryGetValue("MemAvailable", out var availableKb))
            {
                values.TryGetValue("MemFree", out var freeKb);
                values.TryGetValue("Buffers", out var buffersKb);
                values.TryGetValue("Cached", out var cachedKb);
                availableKb = freeKb + buffersKb + cachedKb;
            }

            return Validation<Error, MemoryMetrics>.Success(
                new MemoryMetrics(totalKb * 1024, Math.Min(availableKb, totalKb) * 1024));
        });
    }

    /// <inheritdoc/>
    public Task<Validation<Error, DiskMetrics>> GetDiskAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var probePath = ResolveExistingPath(path);
            var drive = OpenDrive(probePath);

            if (drive is null || !drive.IsReady)
            {
                return Task.FromResult(Fail<DiskMetrics>(
                    "SYSTEM_DISK_UNAVAILABLE",
                    $"Aucune partition exploitable pour « {path} »"));
            }

            var metrics = new DiskMetrics(path, drive.TotalSize, drive.AvailableFreeSpace);
            return Task.FromResult(Validation<Error, DiskMetrics>.Success(metrics));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lecture de l'espace disque impossible pour {Path}", path);
            return Task.FromResult(Fail<DiskMetrics>(
                "SYSTEM_DISK_UNAVAILABLE",
                $"Espace disque illisible pour « {path} » : {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, UptimeMetrics>> GetUptimeAsync(
        CancellationToken cancellationToken = default)
    {
        var processUptime = GetProcessUptime();

        // /proc/uptime est la source la plus fiable ; à défaut (Windows, conteneur
        // restreint) le compteur de ticks du runtime donne une approximation correcte.
        if (File.Exists(_paths.Uptime))
        {
            var content = await ReadFileAsync(_paths.Uptime, "SYSTEM_UPTIME_UNAVAILABLE", cancellationToken);

            var parsed = content.Bind(raw =>
            {
                var firstField = raw
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();

                if (firstField is null || !TryParseDouble(firstField, out var seconds) || seconds < 0)
                {
                    return Fail<UptimeMetrics>(
                        "SYSTEM_UPTIME_INVALID",
                        $"Contenu d'uptime invalide : « {raw.Trim()} »");
                }

                return Validation<Error, UptimeMetrics>.Success(
                    new UptimeMetrics(TimeSpan.FromSeconds(seconds), processUptime));
            });

            if (parsed.IsSuccess)
                return parsed;
        }

        return Validation<Error, UptimeMetrics>.Success(
            new UptimeMetrics(TimeSpan.FromMilliseconds(Environment.TickCount64), processUptime));
    }

    /// <inheritdoc/>
    public string GetApplicationVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
            return assembly.GetName().Version?.ToString() ?? "0.0.0";

        // Les métadonnées de build (+sha) ne sont pas pertinentes à l'affichage.
        var separatorIndex = informational.IndexOf('+');
        return separatorIndex >= 0 ? informational[..separatorIndex] : informational;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Lit un pseudo-fichier système en convertissant toute défaillance en erreur métier.
    /// </summary>
    private async Task<Validation<Error, string>> ReadFileAsync(
        string path,
        string errorCode,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                _logger.LogDebug("Source de métrique absente : {Path}", path);
                return Fail<string>(errorCode, $"Source système absente sur cette plateforme : {path}");
            }

            return Validation<Error, string>.Success(await File.ReadAllTextAsync(path, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lecture impossible de la source de métrique {Path}", path);
            return Fail<string>(errorCode, $"Lecture impossible de {path} : {ex.Message}");
        }
    }

    /// <summary>
    /// Parse les lignes « Clé: valeur kB » de /proc/meminfo.
    /// </summary>
    internal static Dictionary<string, long> ParseMemInfo(string content)
    {
        var values = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var valuePart = line[(separatorIndex + 1)..]
                .Replace("kB", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (long.TryParse(valuePart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                values[key] = value;
        }

        return values;
    }

    /// <summary>
    /// Remonte l'arborescence jusqu'au premier répertoire existant : le répertoire
    /// de données peut ne pas encore être créé au premier démarrage.
    /// </summary>
    internal static string ResolveExistingPath(string path)
    {
        var current = Path.GetFullPath(path);

        while (!Directory.Exists(current) && !File.Exists(current))
        {
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent == current)
                break;

            current = parent;
        }

        return current;
    }

    /// <summary>
    /// Ouvre la partition contenant le chemin fourni.
    /// Sous Unix, <see cref="DriveInfo"/> accepte n'importe quel chemin ; sous Windows
    /// il exige la racine du volume.
    /// </summary>
    private static DriveInfo? OpenDrive(string path)
    {
        try
        {
            return new DriveInfo(path);
        }
        catch (ArgumentException)
        {
            var root = Path.GetPathRoot(path);
            return string.IsNullOrEmpty(root) ? null : new DriveInfo(root);
        }
    }

    private static TimeSpan GetProcessUptime()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var uptime = DateTime.Now - process.StartTime;
            return uptime < TimeSpan.Zero ? TimeSpan.Zero : uptime;
        }
        catch (Exception)
        {
            // Certains environnements conteneurisés restreignent l'accès au procfs.
            return TimeSpan.Zero;
        }
    }

    private static bool TryParseDouble(string value, out double result)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static Validation<Error, T> Fail<T>(string code, string message)
        => Validation<Error, T>.Fail(LanguageExt.Prelude.Seq1(Error.Validation(code, message)));
}
