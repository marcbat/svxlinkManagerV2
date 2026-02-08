using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Events;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;
using DomainError = SvxlinkManagerV2.Domain.Common.Error;

namespace SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;

/// <summary>
/// Handler Wolverine qui réagit à l'événement SalonActivated.
/// Orchestre la configuration complète :
/// 1. Fusion paramètres Salon + SA818
/// 2. Application au hardware SA818
/// 3. Génération svxlink.conf
/// 4. Redémarrage daemon SVXLink
/// </summary>
public static class SalonActivatedHandler
{
    /// <summary>
    /// Traite l'événement SalonActivated (side-effect).
    /// </summary>
    /// <param name="event">Événement d'activation du Salon</param>
    /// <param name="salonRepository">Repository pour charger le Salon</param>
    /// <param name="sa818Repository">Repository pour charger la configuration SA818</param>
    /// <param name="sa818Service">Service de communication avec le module SA818</param>
    /// <param name="configurationService">Service de génération du fichier svxlink.conf</param>
    /// <param name="daemonService">Service de contrôle du daemon SVXLink</param>
    /// <param name="logger">Logger pour traçage</param>
    /// <param name="cancellationToken">Token d'annulation</param>
    /// <returns>Validation indiquant le succès ou l'erreur</returns>
    public static async Task<Validation<LanguageExt.Common.Error, Unit>> Handle(
        SalonActivated @event,
        ISalonRepository salonRepository,
        ISA818Repository sa818Repository,
        ISA818Service sa818Service,
        ISvxLinkConfigurationService configurationService,
        ISvxLinkDaemonService daemonService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Début du side-effect SalonActivated pour Salon {SalonId}",
            @event.Id);

        try
        {
            // Étape 1 : Charger le SalonAggregate
            logger.LogDebug("Chargement du SalonAggregate {SalonId}", @event.Id);
            var salonResult = await salonRepository.GetByIdAsync(@event.Id, cancellationToken);

            if (salonResult.IsFail)
            {
                logger.LogError(
                    "Impossible de charger le Salon {SalonId}",
                    @event.Id);
                return salonResult.Match(
                    Succ: _ => throw new InvalidOperationException(),
                    Fail: errors => Validation<Error, Unit>.Fail(errors));
            }

            var salon = salonResult.Match(
                Succ: s => s,
                Fail: _ => throw new InvalidOperationException());

            logger.LogDebug(
                "Salon {SalonName} chargé avec succès",
                salon.Name);

            // Étape 2 : Charger la configuration SA818
            logger.LogDebug("Chargement de la configuration SA818");
            var sa818Config = await sa818Repository.GetConfigurationAsync(cancellationToken);

            if (sa818Config == null)
            {
                logger.LogError("Configuration SA818 introuvable");
                return Validation<Error, Unit>.Fail(
                    Seq1(new Error("SA818_CONFIG_NOT_FOUND", "Configuration SA818 introuvable")));
            }

            logger.LogDebug(
                "Configuration SA818 chargée : Volume={Volume}, Squelch={Squelch}, Bandwidth={Bandwidth}",
                sa818Config.Volume,
                sa818Config.Squelch,
                sa818Config.Bandwidth);

            // Étape 3 : Construire les commandes AT en fusionnant Salon + SA818
            logger.LogDebug("Construction des commandes AT");
            var commandSet = BuildSA818Commands(salon, sa818Config, logger);

            logger.LogInformation(
                "Commandes AT générées : DmoSetGroup={DmoSetGroup}, DmoSetVolume={DmoSetVolume}, SetFilter={SetFilter}",
                commandSet.DmoSetGroup,
                commandSet.DmoSetVolume,
                commandSet.SetFilter);

            // Étape 4 : Appliquer la configuration au module SA818
            logger.LogInformation("Application de la configuration au module SA818");
            var sa818Result = await sa818Service.ConfigureAsync(commandSet, cancellationToken);

            if (sa818Result.IsFail)
            {
                logger.LogError("Échec de la configuration du module SA818");
                return sa818Result;
            }

            logger.LogInformation("Module SA818 configuré avec succès");

            // Étape 5 : Générer le fichier svxlink.conf
            const string svxlinkConfPath = "/etc/svxlink/svxlink.conf";
            logger.LogInformation(
                "Génération du fichier de configuration SVXLink : {Path}",
                svxlinkConfPath);

            var configResult = await configurationService.GenerateAsync(
                salon,
                svxlinkConfPath,
                cancellationToken);

            if (configResult.IsFail)
            {
                logger.LogError("Échec de la génération du fichier svxlink.conf");
                return configResult;
            }

            logger.LogInformation("Fichier svxlink.conf généré avec succès");

            // Étape 6 : Redémarrer le daemon SVXLink
            logger.LogInformation("Redémarrage du daemon SVXLink");
            var daemonResult = await daemonService.RestartAsync(cancellationToken);

            if (daemonResult.IsFail)
            {
                logger.LogError("Échec du redémarrage du daemon SVXLink");
                return daemonResult;
            }

            logger.LogInformation(
                "Side-effect SalonActivated terminé avec succès pour Salon {SalonId}",
                @event.Id);

            return unit;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Erreur inattendue lors du traitement de SalonActivated pour Salon {SalonId}",
                @event.Id);

            return Validation<Error, Unit>.Fail(
                Seq1(new Error("SALON_ACTIVATED_HANDLER_ERROR", ex.Message)));
        }
    }

    /// <summary>
    /// Construit les 3 commandes AT pour le module SA818 en fusionnant
    /// les paramètres du Salon (fréquences/CTCSS) et du SA818 (volume/squelch/filtres).
    /// </summary>
    /// <param name="salon">Salon contenant les fréquences et CTCSS</param>
    /// <param name="sa818Config">Configuration SA818 contenant volume/squelch/filtres</param>
    /// <param name="logger">Logger pour traçage</param>
    /// <returns>Ensemble de commandes AT prêtes à être envoyées au SA818</returns>
    private static SA818CommandSet BuildSA818Commands(
        SalonAggregate salon,
        Application.Features.SA818.SA818ConfigurationDto sa818Config,
        ILogger logger)
    {
        // Conversion des fréquences CTCSS (Hz) en codes SA818 (0000-0038)
        var txCtcssCode = CtcssMapper.FrequencyToCode(salon.Configuration.TxCtcss);
        var rxCtcssCode = CtcssMapper.FrequencyToCode(salon.Configuration.RxCtcss);

        logger.LogDebug(
            "Conversion CTCSS : TxCtcss={TxCtcssHz}Hz -> {TxCtcssCode}, RxCtcss={RxCtcssHz}Hz -> {RxCtcssCode}",
            salon.Configuration.TxCtcss,
            txCtcssCode,
            salon.Configuration.RxCtcss,
            rxCtcssCode);

        // Conversion de la largeur de bande en int (0 = 12.5kHz, 1 = 25kHz)
        var bandwidthValue = sa818Config.Bandwidth == SA818Bandwidth.Narrow12_5kHz ? 0 : 1;

        // Commande 1 : AT+DMOSETGROUP
        // Format : AT+DMOSETGROUP={Bandwidth},{TxFreq},{RxFreq},{TxCtcss},{Squelch},{RxCtcss}
        // Exemple : AT+DMOSETGROUP=1,145.5500,145.5500,0021,4,0021
        var dmoSetGroup = $"AT+DMOSETGROUP=" +
                          $"{bandwidthValue}," +
                          $"{salon.Configuration.TxFrequency:F4}," +
                          $"{salon.Configuration.RxFrequency:F4}," +
                          $"{txCtcssCode}," +
                          $"{sa818Config.Squelch}," +
                          $"{rxCtcssCode}";

        // Commande 2 : AT+DMOSETVOLUME
        // Format : AT+DMOSETVOLUME={Volume}
        // Exemple : AT+DMOSETVOLUME=4
        var dmoSetVolume = $"AT+DMOSETVOLUME={sa818Config.Volume}";

        // Commande 3 : AT+SETFILTER
        // Format : AT+SETFILTER={PreEmph},{HighPass},{LowPass}
        // Exemple : AT+SETFILTER=1,1,0
        var setFilter = $"AT+SETFILTER=" +
                        $"{(sa818Config.PreEmph ? 1 : 0)}," +
                        $"{(sa818Config.HighPass ? 1 : 0)}," +
                        $"{(sa818Config.LowPass ? 1 : 0)}";

        logger.LogDebug(
            "Commandes AT construites : DmoSetGroup={DmoSetGroup}, DmoSetVolume={DmoSetVolume}, SetFilter={SetFilter}",
            dmoSetGroup,
            dmoSetVolume,
            setFilter);

        return new SA818CommandSet(dmoSetGroup, dmoSetVolume, setFilter);
    }
}
