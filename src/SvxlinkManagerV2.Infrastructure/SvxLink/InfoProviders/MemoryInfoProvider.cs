using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

/// <summary>
/// Fournisseur d'information pour la commande DTMF 303.
/// Annonce la mémoire disponible et le taux d'occupation de la machine.
/// </summary>
public class MemoryInfoProvider : IInfoProvider
{
    private readonly ISystemMetricsService _metrics;
    private readonly ILogger<MemoryInfoProvider> _logger;

    /// <inheritdoc/>
    public int DtmfCode => 303;

    /// <inheritdoc/>
    public string Description => "Mémoire disponible";

    public MemoryInfoProvider(
        ISystemMetricsService metrics,
        ILogger<MemoryInfoProvider> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, string>> GetInfoTextAsync(CancellationToken cancellationToken = default)
    {
        var result = await _metrics.GetMemoryAsync(cancellationToken);

        return result.Match(
            Succ: memory =>
            {
                _logger.LogInformation("Mémoire utilisée : {Percent}%", memory.UsedPercent);

                var available = InfoTextFormatter.Bytes(memory.AvailableBytes);
                var total = InfoTextFormatter.Bytes(memory.TotalBytes);
                var percent = InfoTextFormatter.Round(memory.UsedPercent);

                return Validation<Error, string>.Success(
                    $"La mémoire disponible est de {available} sur {total}, soit {percent} pour cent utilisés");
            },
            Fail: errors => InfoProviderFailure.From(errors, _logger, Description));
    }
}
