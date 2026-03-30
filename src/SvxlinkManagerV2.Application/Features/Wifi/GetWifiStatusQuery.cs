using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Wifi;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.Wifi;

/// <summary>
/// Query pour récupérer l'état WiFi courant (scan + fusion réseaux/profils).
/// </summary>
public record GetWifiStatusQuery() : IRequest<Validation<Error, WifiStatus>>;

/// <summary>
/// Handler pour la query GetWifiStatusQuery.
/// Scanne les réseaux disponibles, récupère les connexions sauvegardées,
/// puis fusionne les données pour construire le statut complet.
/// </summary>
public class GetWifiStatusQueryHandler : IRequestHandler<GetWifiStatusQuery, Validation<Error, WifiStatus>>
{
    private readonly IWifiService _wifiService;

    public GetWifiStatusQueryHandler(IWifiService wifiService)
    {
        _wifiService = wifiService;
    }

    public async Task<Validation<Error, WifiStatus>> Handle(
        GetWifiStatusQuery request,
        CancellationToken cancellationToken)
    {
        var networksResult = await _wifiService.ScanNetworksAsync(cancellationToken);
        if (networksResult.IsFail)
            return networksResult.Map(_ => default(WifiStatus)!);

        var connectionsResult = await _wifiService.GetSavedConnectionsAsync(cancellationToken);
        if (connectionsResult.IsFail)
            return connectionsResult.Map(_ => default(WifiStatus)!);

        var networks = networksResult.Match(
            Succ: n => n,
            Fail: _ => (IReadOnlyList<WifiNetwork>)new List<WifiNetwork>());

        var connections = connectionsResult.Match(
            Succ: c => c,
            Fail: _ => (IReadOnlyList<WifiConnection>)new List<WifiConnection>());

        // Fusionner réseaux et profils sauvegardés par SSID
        var enrichedNetworks = networks
            .Select(n =>
            {
                var savedConn = connections.FirstOrDefault(c =>
                    string.Equals(c.Name, n.Ssid, StringComparison.OrdinalIgnoreCase));
                if (savedConn != null)
                    return n with { HasSavedProfile = true, ConnectionUuid = savedConn.Uuid };
                return n;
            })
            .ToList()
            .AsReadOnly();

        var connectedNetwork = enrichedNetworks.FirstOrDefault(n => n.InUse);
        var isConnected = connectedNetwork != null;

        var status = new WifiStatus(
            IsConnected: isConnected,
            ConnectedSsid: connectedNetwork?.Ssid,
            Signal: connectedNetwork?.Signal,
            Networks: enrichedNetworks);

        return Validation<Error, WifiStatus>.Success(status);
    }
}
