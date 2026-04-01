using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Salons.ActivateParrot;

/// <summary>
/// Commande pour activer le mode Perroquet (Parrot) — répétition audio locale via ModuleParrot.
/// Aucun paramètre requis : le Perroquet n'est pas un salon persisté.
/// </summary>
public record ActivateParrotCommand() : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande ActivateParrotCommand.
/// Orchestre : arrêt du salon/perroquet actif (si existant) → génération config Parrot → restart daemon → activation DTMF.
/// Note : le SA818 n'est PAS reconfiguré — les fréquences du dernier salon actif sont conservées.
/// </summary>
public class ActivateParrotCommandHandler : IRequestHandler<ActivateParrotCommand, Validation<Error, Unit>>
{
    private const string SvxLinkConfPath = "/etc/svxlink/svxlink.conf";
    /// <summary>
    /// Commande DTMF d'activation du ModuleParrot : ID=2 (défini dans svxlink.conf [ModuleParrot]),
    /// suivi de '#' pour valider la sélection.
    /// </summary>
    private const string ParrotDtmfCommand = "2#";

    private readonly IActiveSessionTracker _tracker;
    private readonly ISvxLinkParrotConfigurationService _parrotConfigurationService;
    private readonly ISvxLinkDaemonService _daemonService;
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly ILogger<ActivateParrotCommandHandler> _logger;

    public ActivateParrotCommandHandler(
        IActiveSessionTracker tracker,
        ISvxLinkParrotConfigurationService parrotConfigurationService,
        ISvxLinkDaemonService daemonService,
        IConnectedNodesService connectedNodesService,
        ILogger<ActivateParrotCommandHandler> logger)
    {
        _tracker = tracker;
        _parrotConfigurationService = parrotConfigurationService;
        _daemonService = daemonService;
        _connectedNodesService = connectedNodesService;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        ActivateParrotCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activation du mode Perroquet (Parrot)");

        // Arrêt du daemon si un salon ou le perroquet est déjà actif
        if (_tracker.ActiveSalonId.HasValue || _tracker.IsParrotActive)
        {
            _logger.LogInformation("Auto-désactivation de l'état actif avant activation du Perroquet");

            var stopResult = await _daemonService.StopAsync(cancellationToken);
            if (stopResult.IsFail)
                return Error.Validation("SVXLINK_STOP_ERROR", "Impossible d'arrêter le daemon SVXLink").ToFailure<Unit>();

            _connectedNodesService.Reset();
            _tracker.SetActiveSalon(null);
            _tracker.SetParrotActive(false);
        }

        _logger.LogInformation("Génération de la configuration SVXLink pour le mode Perroquet");
        var configResult = await _parrotConfigurationService.GenerateAsync(SvxLinkConfPath, cancellationToken);
        if (configResult.IsFail)
            return Error.Validation("SVXLINK_CONFIG_ERROR", "Impossible de générer la configuration Perroquet").ToFailure<Unit>();

        _logger.LogInformation("Redémarrage du daemon SVXLink en mode Perroquet");
        var daemonResult = await _daemonService.RestartAsync(cancellationToken);
        if (daemonResult.IsFail)
            return Error.Validation("SVXLINK_RESTART_ERROR", "Impossible de redémarrer le daemon SVXLink").ToFailure<Unit>();

        _logger.LogInformation("Activation du ModuleParrot via commande DTMF '{Command}'", ParrotDtmfCommand);
        var dtmfResult = await _daemonService.SendDtmfCommandAsync(ParrotDtmfCommand, cancellationToken);
        if (dtmfResult.IsFail)
        {
            _logger.LogWarning("Échec de l'activation DTMF du Perroquet — le mode reste actif mais le module peut nécessiter une activation manuelle");
        }

        _tracker.SetParrotActive(true);

        _logger.LogInformation("Mode Perroquet activé avec succès");
        return unit.ToSuccess();
    }
}
