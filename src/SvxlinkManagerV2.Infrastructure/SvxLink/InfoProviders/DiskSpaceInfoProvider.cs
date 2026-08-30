using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Features.SystemStatus;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

/// <summary>
/// Fournisseur d'information pour la commande DTMF 304.
/// Annonce l'espace libre de la partition système — celle qui héberge le système
/// d'exploitation sur la carte SD de la cible Orange Pi.
/// </summary>
public class DiskSpaceInfoProvider : IInfoProvider
{
    private readonly ISystemMetricsService _metrics;
    private readonly SystemMonitoringOptions _options;
    private readonly ILogger<DiskSpaceInfoProvider> _logger;

    /// <inheritdoc/>
    public int DtmfCode => 304;

    /// <inheritdoc/>
    public string Description => "Espace disque disponible";

    public DiskSpaceInfoProvider(
        ISystemMetricsService metrics,
        IOptions<SystemMonitoringOptions> options,
        ILogger<DiskSpaceInfoProvider> logger)
    {
        _metrics = metrics;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, string>> GetInfoTextAsync(CancellationToken cancellationToken = default)
    {
        var result = await _metrics.GetDiskAsync(_options.SystemMountPath, cancellationToken);

        return result.Match(
            Succ: disk =>
            {
                _logger.LogInformation("Espace disque utilisé sur {Path} : {Percent}%",
                    disk.MountPath, disk.UsedPercent);

                var available = InfoTextFormatter.Bytes(disk.AvailableBytes);
                var total = InfoTextFormatter.Bytes(disk.TotalBytes);
                var percent = InfoTextFormatter.Round(disk.UsedPercent);

                return Validation<Error, string>.Success(
                    $"L'espace disque disponible est de {available} sur {total}, soit {percent} pour cent utilisés");
            },
            Fail: errors => InfoProviderFailure.From(errors, _logger, Description));
    }
}
