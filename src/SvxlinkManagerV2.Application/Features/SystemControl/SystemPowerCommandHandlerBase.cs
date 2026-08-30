using LanguageExt;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using DaemonError = LanguageExt.Common.Error;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.SystemControl;

/// <summary>
/// Socle commun aux commandes d'alimentation (redémarrage / arrêt) : vérification de la disponibilité
/// de la plateforme, puis arrêt propre des daemons SVXLink et svxreflector avant l'appel système.
/// </summary>
public abstract class SystemPowerCommandHandlerBase
{
    /// <summary>
    /// Délai maximum accordé à l'arrêt des daemons : passé ce délai, l'action d'alimentation
    /// est déclenchée malgré tout pour ne pas laisser l'utilisateur sans recours.
    /// </summary>
    private const int DaemonStopTimeoutSeconds = 20;

    private readonly ISystemControlService _systemControlService;
    private readonly ISvxLinkDaemonService _svxLinkDaemonService;
    private readonly IReflectorDaemonService _reflectorDaemonService;
    private readonly ILogger _logger;

    protected SystemPowerCommandHandlerBase(
        ISystemControlService systemControlService,
        ISvxLinkDaemonService svxLinkDaemonService,
        IReflectorDaemonService reflectorDaemonService,
        ILogger logger)
    {
        _systemControlService = systemControlService;
        _svxLinkDaemonService = svxLinkDaemonService;
        _reflectorDaemonService = reflectorDaemonService;
        _logger = logger;
    }

    /// <summary>
    /// Vérifie la disponibilité, arrête les daemons puis déclenche l'action d'alimentation.
    /// </summary>
    /// <param name="actionLabel">Libellé de l'action, utilisé pour la journalisation</param>
    /// <param name="systemAction">Appel système à déclencher une fois les daemons arrêtés</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant que l'action a bien été planifiée</returns>
    protected async Task<Validation<Error, Unit>> ExecuteAsync(
        string actionLabel,
        Func<CancellationToken, Task<Validation<Error, Unit>>> systemAction,
        CancellationToken cancellationToken)
    {
        var availability = _systemControlService.GetAvailability();
        if (!availability.IsSupported)
        {
            _logger.LogWarning(
                "Action d'alimentation ({Action}) refusée : {Reason}",
                actionLabel, availability.UnsupportedReason);

            return Error.Validation(
                    "SYSTEM_CONTROL_UNSUPPORTED",
                    availability.UnsupportedReason
                        ?? "Le contrôle de l'alimentation n'est pas disponible sur cette plateforme.")
                .ToFailure<Unit>();
        }

        _logger.LogWarning("{Action} de la machine demandé depuis l'interface", actionLabel);

        await StopDaemonsAsync(cancellationToken);

        return await systemAction(cancellationToken);
    }

    /// <summary>
    /// Arrête les daemons au mieux : un échec est journalisé mais n'empêche pas l'action d'alimentation,
    /// sinon l'utilisateur n'aurait d'autre recours que la coupure secteur.
    /// </summary>
    private async Task StopDaemonsAsync(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(DaemonStopTimeoutSeconds));

        await StopDaemonSafelyAsync("SVXLink", () => _svxLinkDaemonService.StopAsync(cts.Token));
        await StopDaemonSafelyAsync("svxreflector", () => _reflectorDaemonService.StopAsync(cts.Token));
    }

    private async Task StopDaemonSafelyAsync(
        string daemonName,
        Func<Task<Validation<DaemonError, Unit>>> stop)
    {
        try
        {
            var result = await stop();

            result.Match(
                Succ: _ => _logger.LogInformation(
                    "Daemon {Daemon} arrêté avant l'action d'alimentation", daemonName),
                Fail: errors => _logger.LogWarning(
                    "Arrêt du daemon {Daemon} en échec avant l'action d'alimentation : {Errors}",
                    daemonName, string.Join(" ", errors.Select(e => e.Message))));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception lors de l'arrêt du daemon {Daemon}", daemonName);
        }
    }
}
