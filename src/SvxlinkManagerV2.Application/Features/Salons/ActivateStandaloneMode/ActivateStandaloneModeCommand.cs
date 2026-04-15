using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Salons.ActivateStandaloneMode;

/// <summary>
/// Commande pour activer SVXLink en mode simplex (sans réflecteur).
/// Ce mode est utilisé pour l'écoute DTMF sans connexion à un réflecteur.
/// Utilise les fréquences RX/TX définies dans la configuration générale.
/// </summary>
public record ActivateStandaloneModeCommand() : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande ActivateStandaloneModeCommand.
/// Configure le SA818 et génère un svxlink.conf en mode simplex (sans ReflectorLogic).
/// </summary>
public class ActivateStandaloneModeCommandHandler : IRequestHandler<ActivateStandaloneModeCommand, Validation<Error, Unit>>
{
    private const string SvxLinkConfPath = "/etc/svxlink/svxlink.conf";

    private readonly IGeneralConfigurationRepository _generalConfigRepository;
    private readonly ISA818Repository _sa818Repository;
    private readonly ISA818Service _sa818Service;
    private readonly ISvxLinkConfigurationService _configurationService;
    private readonly ISvxLinkDaemonService _daemonService;
    private readonly IActiveSessionTracker _tracker;
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly ILogger<ActivateStandaloneModeCommandHandler> _logger;

    public ActivateStandaloneModeCommandHandler(
        IGeneralConfigurationRepository generalConfigRepository,
        ISA818Repository sa818Repository,
        ISA818Service sa818Service,
        ISvxLinkConfigurationService configurationService,
        ISvxLinkDaemonService daemonService,
        IActiveSessionTracker tracker,
        IConnectedNodesService connectedNodesService,
        ILogger<ActivateStandaloneModeCommandHandler> logger)
    {
        _generalConfigRepository = generalConfigRepository;
        _sa818Repository = sa818Repository;
        _sa818Service = sa818Service;
        _configurationService = configurationService;
        _daemonService = daemonService;
        _tracker = tracker;
        _connectedNodesService = connectedNodesService;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        ActivateStandaloneModeCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activation du mode standalone (simplex sans réflecteur)");

        var generalConfig = await _generalConfigRepository.GetAsync(cancellationToken);
        var rxFrequency = generalConfig?.DefaultRxFrequency ?? 145.550m;
        var txFrequency = generalConfig?.DefaultTxFrequency ?? 145.550m;

        var currentActiveSalonId = _tracker.ActiveSalonId;
        if (currentActiveSalonId.HasValue)
        {
            _logger.LogInformation(
                "Auto-désactivation du salon actif {OldSalonId} avant activation du mode standalone",
                currentActiveSalonId.Value);

            var stopResult = await _daemonService.StopAsync(cancellationToken);
            if (stopResult.IsFail)
                return Error.Validation("SVXLINK_STOP_ERROR", "Impossible d'arrêter le daemon SVXLink").ToFailure<Unit>();

            _connectedNodesService.Reset();
            _tracker.SetActiveSalon(null);
        }

        var sa818Config = await _sa818Repository.GetConfigurationAsync(cancellationToken);
        if (sa818Config != null)
        {
            _logger.LogInformation(
                "Configuration du module SA818 en mode standalone (RX: {RxFreq} MHz, TX: {TxFreq} MHz)",
                rxFrequency, txFrequency);

            var commandSet = BuildSA818Commands(rxFrequency, txFrequency, sa818Config, _logger);
            var sa818Result = await _sa818Service.ConfigureAsync(commandSet, cancellationToken);
            if (sa818Result.IsFail)
            {
                _logger.LogWarning(
                    "Échec de la configuration SA818 en mode standalone, activation continue sans SA818");
            }
        }
        else
        {
            _logger.LogInformation("Configuration SA818 introuvable, démarrage SVXLink sans configuration SA818");
        }

        _logger.LogInformation("Génération du fichier {Path} en mode standalone", SvxLinkConfPath);
        var configResult = await _configurationService.GenerateStandaloneAsync(
            rxFrequency, txFrequency, SvxLinkConfPath, cancellationToken);
        if (configResult.IsFail)
            return Error.Validation("SVXLINK_CONFIG_ERROR", "Impossible de générer le fichier svxlink.conf en mode standalone").ToFailure<Unit>();

        _logger.LogInformation("Démarrage du daemon SVXLink en mode standalone (version legacy)");
        var daemonResult = await _daemonService.RestartAsync(ReflectorProtocol.V2, cancellationToken);
        if (daemonResult.IsFail)
            return Error.Validation("SVXLINK_RESTART_ERROR", "Impossible de démarrer le daemon SVXLink en mode standalone").ToFailure<Unit>();

        _logger.LogInformation(
            "Mode standalone activé avec succès (RX: {RxFreq} MHz, TX: {TxFreq} MHz)",
            rxFrequency, txFrequency);
        return unit.ToSuccess();
    }

    private static SA818CommandSet BuildSA818Commands(
        decimal rxFrequency,
        decimal txFrequency,
        SA818ConfigurationDto sa818Config,
        ILogger logger)
    {
        var bandwidthValue = sa818Config.Bandwidth == SA818Bandwidth.Narrow12_5kHz ? 0 : 1;

        var dmoSetGroup = $"AT+DMOSETGROUP=" +
                          $"{bandwidthValue}," +
                          $"{txFrequency:F4}," +
                          $"{rxFrequency:F4}," +
                          $"0000," +
                          $"{sa818Config.Squelch}," +
                          $"0000";

        var dmoSetVolume = $"AT+DMOSETVOLUME={sa818Config.Volume}";

        var setFilter = $"AT+SETFILTER=" +
                        $"{(sa818Config.PreEmph ? 1 : 0)}," +
                        $"{(sa818Config.HighPass ? 1 : 0)}," +
                        $"{(sa818Config.LowPass ? 1 : 0)}";

        logger.LogDebug(
            "Commandes AT standalone : DmoSetGroup={DmoSetGroup}, DmoSetVolume={DmoSetVolume}, SetFilter={SetFilter}",
            dmoSetGroup, dmoSetVolume, setFilter);

        return new SA818CommandSet(dmoSetGroup, dmoSetVolume, setFilter);
    }
}
