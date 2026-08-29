using LanguageExt;
using MediatR;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Wifi;

namespace SvxlinkManagerV2.Application.Features.SystemStatus.GetSystemStatus;

/// <summary>
/// Query d'agrégation de l'état système : température, charge, mémoire, disques,
/// uptime, lien réseau et versions logicielles.
/// </summary>
public record GetSystemStatusQuery() : IRequest<SystemStatusDto>;

/// <summary>
/// Handler pour la query GetSystemStatusQuery.
/// Interroge chaque métrique indépendamment : un échec de lecture est converti en
/// métrique « indisponible » plutôt qu'en échec global, afin que la page reste utilisable
/// sur une plateforme où certaines sources ne sont pas exposées (Windows, conteneur restreint).
/// </summary>
public class GetSystemStatusQueryHandler : IRequestHandler<GetSystemStatusQuery, SystemStatusDto>
{
    private readonly ISystemMetricsService _metrics;
    private readonly IWifiService _wifiService;
    private readonly ISvxLinkStrategyResolver _strategyResolver;
    private readonly SystemMonitoringOptions _options;

    public GetSystemStatusQueryHandler(
        ISystemMetricsService metrics,
        IWifiService wifiService,
        ISvxLinkStrategyResolver strategyResolver,
        IOptions<SystemMonitoringOptions> options)
    {
        _metrics = metrics;
        _wifiService = wifiService;
        _strategyResolver = strategyResolver;
        _options = options.Value;
    }

    public async Task<SystemStatusDto> Handle(
        GetSystemStatusQuery request,
        CancellationToken cancellationToken)
    {
        var temperature = ToValueMetric(
            await _metrics.GetCpuTemperatureCelsiusAsync(cancellationToken),
            celsius => Threshold(
                celsius,
                _options.CpuTemperatureWarningCelsius,
                _options.CpuTemperatureCriticalCelsius));

        var load = ToMetric(
            await _metrics.GetCpuLoadAsync(cancellationToken),
            l => Threshold(l.LoadPercent, _options.CpuLoadWarningPercent, _options.CpuLoadCriticalPercent));

        var memory = ToMetric(
            await _metrics.GetMemoryAsync(cancellationToken),
            m => Threshold(m.UsedPercent, _options.MemoryUsageWarningPercent, _options.MemoryUsageCriticalPercent));

        var uptime = ToMetric(
            await _metrics.GetUptimeAsync(cancellationToken),
            _ => MetricLevel.Normal);

        var disks = new List<DiskStatusDto>
        {
            await ReadDiskAsync("Partition système", _options.SystemMountPath, cancellationToken),
            await ReadDiskAsync("Partition de données", _options.DataPath, cancellationToken)
        };

        var network = ToMetric(
            await _wifiService.GetActiveLinkAsync(cancellationToken),
            link => link.SignalPercent is int signal && signal < _options.WifiSignalWarningPercent
                ? MetricLevel.Warning
                : MetricLevel.Normal);

        var installations = _strategyResolver.GetAll()
            // Ordre chronologique des versions : l'énumération ReflectorProtocol place V3 avant V2.
            .OrderBy(s => s.Version, StringComparer.Ordinal)
            .Select(s => new SvxLinkInstallationDto(
                Name: s.DisplayName,
                Version: s.Version,
                Protocol: s.Protocol.ToString(),
                BinaryPath: s.BinaryPath,
                IsInstalled: s.IsInstalled))
            .ToList()
            .AsReadOnly();

        return new SystemStatusDto(
            CollectedAt: DateTimeOffset.Now,
            CpuTemperatureCelsius: temperature,
            CpuLoad: load,
            Memory: memory,
            Disks: disks.AsReadOnly(),
            Uptime: uptime,
            Network: network,
            ApplicationVersion: _metrics.GetApplicationVersion(),
            SvxLinkInstallations: installations);
    }

    private async Task<DiskStatusDto> ReadDiskAsync(string label, string path, CancellationToken cancellationToken)
    {
        var metric = ToMetric(
            await _metrics.GetDiskAsync(path, cancellationToken),
            d => Threshold(d.UsedPercent, _options.DiskUsageWarningPercent, _options.DiskUsageCriticalPercent));

        return new DiskStatusDto(label, path, metric);
    }

    /// <summary>
    /// Convertit un résultat de lecture en métrique affichable, en calculant son niveau d'alerte.
    /// </summary>
    private static SystemMetric<T> ToMetric<T>(Validation<Error, T> result, Func<T, MetricLevel> levelSelector)
        where T : class
        => result.Match(
            Succ: value => SystemMetric<T>.Available(value, levelSelector(value)),
            Fail: errors => SystemMetric<T>.Unavailable(FormatErrors(errors)));

    /// <summary>
    /// Variante de <see cref="ToMetric{T}"/> pour les métriques numériques.
    /// </summary>
    private static SystemValueMetric ToValueMetric(Validation<Error, double> result, Func<double, MetricLevel> levelSelector)
        => result.Match(
            Succ: value => SystemValueMetric.Available(value, levelSelector(value)),
            Fail: errors => SystemValueMetric.Unavailable(FormatErrors(errors)));

    private static string FormatErrors(Seq<Error> errors)
    {
        var message = string.Join(" | ", errors.Select(e => e.Message));
        return string.IsNullOrWhiteSpace(message) ? "Donnée indisponible sur cette plateforme" : message;
    }

    /// <summary>
    /// Positionne une valeur par rapport aux seuils d'avertissement et critique.
    /// </summary>
    internal static MetricLevel Threshold(double value, double warning, double critical)
    {
        if (value >= critical)
            return MetricLevel.Critical;

        return value >= warning ? MetricLevel.Warning : MetricLevel.Normal;
    }
}
