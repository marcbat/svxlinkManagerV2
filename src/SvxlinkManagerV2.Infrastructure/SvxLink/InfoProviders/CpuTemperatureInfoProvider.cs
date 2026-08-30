using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

/// <summary>
/// Fournisseur d'information pour la commande DTMF 301.
/// Annonce la température du processeur lue par <see cref="ISystemMetricsService"/>.
/// </summary>
public class CpuTemperatureInfoProvider : IInfoProvider
{
    private readonly ISystemMetricsService _metrics;
    private readonly ILogger<CpuTemperatureInfoProvider> _logger;

    /// <inheritdoc/>
    public int DtmfCode => 301;

    /// <inheritdoc/>
    public string Description => "Température du processeur";

    public CpuTemperatureInfoProvider(
        ISystemMetricsService metrics,
        ILogger<CpuTemperatureInfoProvider> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, string>> GetInfoTextAsync(CancellationToken cancellationToken = default)
    {
        var result = await _metrics.GetCpuTemperatureCelsiusAsync(cancellationToken);

        return result.Match(
            Succ: celsius =>
            {
                var rounded = Math.Round(celsius, MidpointRounding.AwayFromZero);
                _logger.LogInformation("Température CPU : {Temp}°C", rounded);

                var degreeWord = InfoTextFormatter.Plural(rounded, "degré", "degrés");
                return Validation<Error, string>.Success(
                    $"La température du processeur est de {InfoTextFormatter.Round(rounded)} {degreeWord}");
            },
            Fail: errors => InfoProviderFailure.From(errors, _logger, Description));
    }
}
