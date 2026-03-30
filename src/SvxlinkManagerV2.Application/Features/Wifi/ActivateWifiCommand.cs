using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.Wifi;

/// <summary>
/// Commande pour activer une connexion WiFi sauvegardée via son UUID NetworkManager.
/// </summary>
/// <param name="Uuid">UUID de la connexion NetworkManager à activer</param>
public record ActivateWifiCommand(string Uuid) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande ActivateWifiCommand.
/// </summary>
public class ActivateWifiCommandHandler : IRequestHandler<ActivateWifiCommand, Validation<Error, Unit>>
{
    private readonly IWifiService _wifiService;

    public ActivateWifiCommandHandler(IWifiService wifiService)
    {
        _wifiService = wifiService;
    }

    public async Task<Validation<Error, Unit>> Handle(
        ActivateWifiCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Uuid))
            return Domain.Common.Error.Validation("WIFI_UUID_REQUIRED", "L'UUID de connexion est requis.").ToFailure<Unit>();

        return await _wifiService.ActivateConnectionAsync(command.Uuid, cancellationToken);
    }
}
