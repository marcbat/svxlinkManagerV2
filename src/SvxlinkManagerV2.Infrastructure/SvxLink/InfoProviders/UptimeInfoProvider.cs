using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

/// <summary>
/// Fournisseur d'information pour la commande DTMF 305.
/// Annonce depuis combien de temps la machine fonctionne.
/// </summary>
public class UptimeInfoProvider : IInfoProvider
{
    private readonly ISystemMetricsService _metrics;
    private readonly ILogger<UptimeInfoProvider> _logger;

    /// <inheritdoc/>
    public int DtmfCode => 305;

    /// <inheritdoc/>
    public string Description => "Durée de fonctionnement";

    public UptimeInfoProvider(
        ISystemMetricsService metrics,
        ILogger<UptimeInfoProvider> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, string>> GetInfoTextAsync(CancellationToken cancellationToken = default)
    {
        var result = await _metrics.GetUptimeAsync(cancellationToken);

        return result.Match(
            Succ: uptime =>
            {
                _logger.LogInformation("Uptime machine : {Machine}", uptime.Machine);

                return Validation<Error, string>.Success(
                    $"La station fonctionne depuis {InfoTextFormatter.Duration(uptime.Machine)}");
            },
            Fail: errors => InfoProviderFailure.From(errors, _logger, Description));
    }
}
