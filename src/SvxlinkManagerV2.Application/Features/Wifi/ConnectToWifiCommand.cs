using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.Wifi;

/// <summary>
/// Commande pour se connecter à un réseau WiFi avec un mot de passe.
/// Utilisée pour les nouveaux réseaux sans profil sauvegardé.
/// </summary>
/// <param name="Ssid">SSID du réseau cible</param>
/// <param name="Password">Mot de passe WPA2 (non loggé, non stocké)</param>
public record ConnectToWifiCommand(string Ssid, string Password) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande ConnectToWifiCommand.
/// </summary>
public class ConnectToWifiCommandHandler : IRequestHandler<ConnectToWifiCommand, Validation<Error, Unit>>
{
    private readonly IWifiService _wifiService;

    public ConnectToWifiCommandHandler(IWifiService wifiService)
    {
        _wifiService = wifiService;
    }

    public async Task<Validation<Error, Unit>> Handle(
        ConnectToWifiCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Ssid))
            return Domain.Common.Error.Validation("WIFI_SSID_REQUIRED", "Le SSID est requis.").ToFailure<Unit>();

        return await _wifiService.ConnectAsync(command.Ssid, command.Password, cancellationToken);
    }
}
