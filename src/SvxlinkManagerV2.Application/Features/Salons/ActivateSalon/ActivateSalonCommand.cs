using LanguageExt;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;

/// <summary>
/// Commande pour activer un Salon (connexion au reflector).
/// Orchestre la configuration SA818, la génération svxlink.conf et le redémarrage du daemon.
/// </summary>
/// <param name="Id">Identifiant unique du salon à activer</param>
public record ActivateSalonCommand(Guid Id);

/// <summary>
/// Handler pour la commande ActivateSalonCommand.
/// Règle métier : un seul salon peut être actif à la fois (géré par IActiveSessionTracker).
/// </summary>
public static class ActivateSalonCommandHandler
{
    private const string SvxLinkConfPath = "/etc/svxlink/svxlink.conf";

    /// <summary>
    /// Active le Salon en effectuant toutes les opérations nécessaires :
    /// auto-désactivation de l'ancien salon actif, configuration SA818, génération svxlink.conf,
    /// redémarrage du daemon SVXLink et mise à jour du tracker d'état runtime.
    /// </summary>
    public static async Task<Validation<Error, Unit>> Handle(
        ActivateSalonCommand command,
        ISalonRepository repository,
        IActiveSessionTracker tracker,
        ISA818Repository sa818Repository,
        ISA818Service sa818Service,
        ISvxLinkConfigurationService configurationService,
        ISvxLinkDaemonService daemonService,
        IConnectedNodesService connectedNodesService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Activation du Salon {SalonId}", command.Id);

        // Étape 1 : Charger l'aggregate
        var aggregateResult = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        if (aggregate.IsDeleted)
            return Error.Validation("SALON_DELETED", "Le salon est supprimé").ToFailure<Unit>();

        // Étape 2 : Si un autre salon est actif, l'arrêter d'abord
        var currentActiveSalonId = tracker.ActiveSalonId;
        if (currentActiveSalonId.HasValue && currentActiveSalonId.Value != command.Id)
        {
            logger.LogInformation(
                "Auto-désactivation du salon actif {OldSalonId} avant activation de {NewSalonId}",
                currentActiveSalonId.Value, command.Id);

            var stopResult = await daemonService.StopAsync(cancellationToken);
            if (stopResult.IsFail)
                return Error.Validation("SVXLINK_STOP_ERROR", "Impossible d'arrêter le daemon SVXLink").ToFailure<Unit>();

            connectedNodesService.Reset();
            tracker.SetActiveSalon(null);
        }

        // Étape 3 : Charger la configuration SA818
        var sa818Config = await sa818Repository.GetConfigurationAsync(cancellationToken);
        if (sa818Config == null)
            return Error.Validation("SA818_CONFIG_NOT_FOUND", "Configuration SA818 introuvable").ToFailure<Unit>();

        // Étape 4 : Configurer le module SA818
        logger.LogInformation("Configuration du module SA818 pour le Salon {SalonName}", aggregate.Name);
        var commandSet = BuildSA818Commands(aggregate, sa818Config, logger);
        var sa818Result = await sa818Service.ConfigureAsync(commandSet, cancellationToken);
        if (sa818Result.IsFail)
            return Error.Validation("SA818_CONFIGURE_ERROR", "Impossible de configurer le module SA818").ToFailure<Unit>();

        // Étape 5 : Générer le fichier svxlink.conf
        logger.LogInformation("Génération du fichier {Path}", SvxLinkConfPath);
        var configResult = await configurationService.GenerateAsync(aggregate, SvxLinkConfPath, cancellationToken);
        if (configResult.IsFail)
            return Error.Validation("SVXLINK_CONFIG_ERROR", "Impossible de générer le fichier svxlink.conf").ToFailure<Unit>();

        // Étape 6 : Redémarrer le daemon SVXLink
        logger.LogInformation("Redémarrage du daemon SVXLink");
        var daemonResult = await daemonService.RestartAsync(cancellationToken);
        if (daemonResult.IsFail)
            return Error.Validation("SVXLINK_RESTART_ERROR", "Impossible de redémarrer le daemon SVXLink").ToFailure<Unit>();

        // Étape 7 : Mettre à jour le tracker d'état runtime
        tracker.SetActiveSalon(command.Id);

        logger.LogInformation("Salon {SalonName} ({SalonId}) activé avec succès", aggregate.Name, command.Id);
        return unit.ToSuccess();
    }

    /// <summary>
    /// Construit les commandes AT pour le module SA818 en fusionnant
    /// les paramètres du Salon (fréquences/CTCSS) et du SA818 (volume/squelch/filtres).
    /// </summary>
    private static SA818CommandSet BuildSA818Commands(
        Domain.Aggregates.Salon.SalonAggregate salon,
        SA818ConfigurationDto sa818Config,
        ILogger logger)
    {
        var txCtcssCode = CtcssMapper.FrequencyToCode(salon.Configuration.TxCtcss);
        var rxCtcssCode = CtcssMapper.FrequencyToCode(salon.Configuration.RxCtcss);
        var bandwidthValue = sa818Config.Bandwidth == SA818Bandwidth.Narrow12_5kHz ? 0 : 1;

        var dmoSetGroup = $"AT+DMOSETGROUP=" +
                          $"{bandwidthValue}," +
                          $"{salon.Configuration.TxFrequency:F4}," +
                          $"{salon.Configuration.RxFrequency:F4}," +
                          $"{txCtcssCode}," +
                          $"{sa818Config.Squelch}," +
                          $"{rxCtcssCode}";

        var dmoSetVolume = $"AT+DMOSETVOLUME={sa818Config.Volume}";

        var setFilter = $"AT+SETFILTER=" +
                        $"{(sa818Config.PreEmph ? 1 : 0)}," +
                        $"{(sa818Config.HighPass ? 1 : 0)}," +
                        $"{(sa818Config.LowPass ? 1 : 0)}";

        logger.LogDebug(
            "Commandes AT : DmoSetGroup={DmoSetGroup}, DmoSetVolume={DmoSetVolume}, SetFilter={SetFilter}",
            dmoSetGroup, dmoSetVolume, setFilter);

        return new SA818CommandSet(dmoSetGroup, dmoSetVolume, setFilter);
    }
}
