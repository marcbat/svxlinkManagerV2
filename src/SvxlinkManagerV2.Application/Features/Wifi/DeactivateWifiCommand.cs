using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.Wifi;

/// <summary>
/// Commande pour désactiver (déconnecter) une connexion WiFi active via son UUID NetworkManager.
/// </summary>
/// <param name="Uuid">UUID de la connexion NetworkManager à désactiver</param>
public record DeactivateWifiCommand(string Uuid) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande DeactivateWifiCommand.
/// </summary>
public class DeactivateWifiCommandHandler : IRequestHandler<DeactivateWifiCommand, Validation<Error, Unit>>
{
    private readonly IWifiService _wifiService;

    public DeactivateWifiCommandHandler(IWifiService wifiService)
    {
        _wifiService = wifiService;
    }

    public async Task<Validation<Error, Unit>> Handle(
        DeactivateWifiCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Uuid))
            return Domain.Common.Error.Validation("WIFI_UUID_REQUIRED", "L'UUID de connexion est requis.").ToFailure<Unit>();

        return await _wifiService.DeactivateConnectionAsync(command.Uuid, cancellationToken);
    }
}
