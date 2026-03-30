using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.Wifi;

/// <summary>
/// Commande pour supprimer un profil de connexion WiFi de NetworkManager.
/// </summary>
/// <param name="Uuid">UUID de la connexion NetworkManager à supprimer</param>
public record DeleteWifiConnectionCommand(string Uuid) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande DeleteWifiConnectionCommand.
/// </summary>
public class DeleteWifiConnectionCommandHandler : IRequestHandler<DeleteWifiConnectionCommand, Validation<Error, Unit>>
{
    private readonly IWifiService _wifiService;

    public DeleteWifiConnectionCommandHandler(IWifiService wifiService)
    {
        _wifiService = wifiService;
    }

    public async Task<Validation<Error, Unit>> Handle(
        DeleteWifiConnectionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Uuid))
            return Domain.Common.Error.Validation("WIFI_UUID_REQUIRED", "L'UUID de connexion est requis.").ToFailure<Unit>();

        return await _wifiService.DeleteConnectionAsync(command.Uuid, cancellationToken);
    }
}
