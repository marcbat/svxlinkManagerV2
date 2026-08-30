using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

/// <summary>
/// Fournisseur d'information pour la commande DTMF 306.
/// Annonce la charge processeur moyenne sur la dernière minute, ramenée
/// au nombre de cœurs de la machine.
/// </summary>
public class CpuLoadInfoProvider : IInfoProvider
{
    private readonly ISystemMetricsService _metrics;
    private readonly ILogger<CpuLoadInfoProvider> _logger;

    /// <inheritdoc/>
    public int DtmfCode => 306;

    /// <inheritdoc/>
    public string Description => "Charge du processeur";

    public CpuLoadInfoProvider(
        ISystemMetricsService metrics,
        ILogger<CpuLoadInfoProvider> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, string>> GetInfoTextAsync(CancellationToken cancellationToken = default)
    {
        var result = await _metrics.GetCpuLoadAsync(cancellationToken);

        return result.Match(
            Succ: load =>
            {
                _logger.LogInformation("Charge CPU : {Percent}%", load.LoadPercent);

                return Validation<Error, string>.Success(
                    $"La charge du processeur est de {InfoTextFormatter.Round(load.LoadPercent)} pour cent");
            },
            Fail: errors => InfoProviderFailure.From(errors, _logger, Description));
    }
}
