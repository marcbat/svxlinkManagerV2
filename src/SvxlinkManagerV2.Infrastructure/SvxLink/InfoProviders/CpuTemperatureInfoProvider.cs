using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

/// <summary>
/// Fournisseur d'information pour la commande DTMF 301.
/// Lit la température du processeur depuis le système de fichiers Linux
/// (<c>/sys/class/thermal/thermal_zone0/temp</c>) et retourne une phrase en français.
/// </summary>
public class CpuTemperatureInfoProvider : IInfoProvider
{
    private readonly ILogger<CpuTemperatureInfoProvider> _logger;
    private readonly string _thermalZonePath;
    internal const string DefaultThermalZonePath = "/sys/class/thermal/thermal_zone0/temp";

    /// <inheritdoc/>
    public int DtmfCode => 301;

    /// <inheritdoc/>
    public string Description => "Température du processeur";

    public CpuTemperatureInfoProvider(ILogger<CpuTemperatureInfoProvider> logger, string? thermalZonePath = null)
    {
        _logger = logger;
        _thermalZonePath = thermalZonePath ?? DefaultThermalZonePath;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, string>> GetInfoTextAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Lecture de la température CPU depuis {Path}", _thermalZonePath);

        try
        {
            if (!File.Exists(_thermalZonePath))
            {
                var errorMsg = $"Fichier de température introuvable : {_thermalZonePath}";
                _logger.LogWarning(errorMsg);
                return Validation<Error, string>.Fail(Seq1(Error.New(errorMsg)));
            }

            var rawContent = await File.ReadAllTextAsync(_thermalZonePath, cancellationToken);

            if (!int.TryParse(rawContent.Trim(), out var rawTemp))
            {
                var errorMsg = $"Valeur de température invalide : « {rawContent.Trim()} »";
                _logger.LogWarning(errorMsg);
                return Validation<Error, string>.Fail(Seq1(Error.New(errorMsg)));
            }

            var tempCelsius = rawTemp / 1000;
            var infoText = $"La température du processeur est de {tempCelsius} degrés";

            _logger.LogInformation("Température CPU : {Temp}°C", tempCelsius);
            return Validation<Error, string>.Success(infoText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de la lecture de la température CPU");
            return Validation<Error, string>.Fail(Seq1(Error.New(ex)));
        }
    }
}
