using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Wifi;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

/// <summary>
/// Fournisseur d'information pour la commande DTMF 303.
/// Annonce l'état réseau courant : mode (client ou point d'accès), SSID et qualité du signal.
/// </summary>
public class NetworkStatusInfoProvider : IInfoProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NetworkStatusInfoProvider> _logger;

    /// <summary>Texte annoncé lorsque aucun réseau WiFi n'est actif.</summary>
    internal const string NoNetworkText = "Le nœud n'est connecté à aucun réseau WiFi";

    /// <inheritdoc/>
    public int DtmfCode => 303;

    /// <inheritdoc/>
    public string Description => "État du réseau WiFi";

    public NetworkStatusInfoProvider(
        IServiceScopeFactory scopeFactory,
        ILogger<NetworkStatusInfoProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, string>> GetInfoTextAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var wifiService = scope.ServiceProvider.GetRequiredService<IWifiService>();

            var scanResult = await wifiService.ScanNetworksAsync(cancellationToken);

            return scanResult.Match(
                Succ: networks =>
                {
                    var active = networks.FirstOrDefault(n => n.InUse);

                    if (active is null)
                        _logger.LogInformation("Commande DTMF 303 : aucun réseau WiFi actif");
                    else
                        _logger.LogInformation("Commande DTMF 303 : réseau {Ssid} ({Signal} %)", active.Ssid, active.Signal);

                    return Validation<Error, string>.Success(BuildInfoText(active));
                },
                Fail: errors =>
                {
                    _logger.LogWarning("Commande DTMF 303 : échec du scan WiFi");
                    return Validation<Error, string>.Fail(
                        errors.Map(e => Error.New(e.Message)));
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de la récupération de l'état réseau");
            return Validation<Error, string>.Fail(Seq1(Error.New(ex)));
        }
    }

    /// <summary>
    /// Construit la phrase annoncée à partir du réseau WiFi actif.
    /// </summary>
    /// <param name="active">Réseau actuellement utilisé, ou <c>null</c> si aucun.</param>
    internal static string BuildInfoText(WifiNetwork? active)
    {
        if (active is null)
            return NoNetworkText;

        var ssid = string.IsNullOrWhiteSpace(active.Ssid) ? "réseau masqué" : active.Ssid;

        return $"Le nœud est en mode {FormatMode(active.Mode)}, connecté au réseau {ssid}. "
             + $"Le niveau de signal est de {active.Signal} pour cent, qualité {FormatQuality(active.Signal)}";
    }

    /// <summary>
    /// Traduit le mode nmcli du réseau actif en libellé annoncé.
    /// nmcli rapporte « Ap » / « Master » lorsque l'interface fait office de point d'accès.
    /// </summary>
    internal static string FormatMode(string? nmcliMode) =>
        nmcliMode?.Trim().ToLowerInvariant() switch
        {
            "ap" or "master" or "access point" => "point d'accès",
            _ => "client"
        };

    /// <summary>
    /// Traduit un niveau de signal en pourcentage en qualité énoncée en phonie.
    /// Les seuils sont alignés sur ceux des barres de signal affichées dans l'interface.
    /// </summary>
    internal static string FormatQuality(int signal) => signal switch
    {
        >= 80 => "excellente",
        >= 60 => "bonne",
        >= 40 => "moyenne",
        >= 20 => "faible",
        _ => "très faible"
    };
}
