using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.SA818;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Salons.ActivateSalon;

/// <summary>
/// Commande pour activer un Salon (connexion au reflector).
/// </summary>
/// <param name="Id">Identifiant unique du salon à activer</param>
public record ActivateSalonCommand(Guid Id) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour la commande ActivateSalonCommand.
/// Orchestre la configuration SA818, le déploiement du son, la génération svxlink.conf et le redémarrage du daemon.
/// </summary>
public class ActivateSalonCommandHandler : IRequestHandler<ActivateSalonCommand, Validation<Error, Unit>>
{
    private const string SvxLinkConfPath = "/etc/svxlink/svxlink.conf";

    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _tracker;
    private readonly ISA818Repository _sa818Repository;
    private readonly ISA818Service _sa818Service;
    private readonly ISvxLinkConfigurationService _configurationService;
    private readonly ISvxLinkDaemonService _daemonService;
    private readonly IConnectedNodesService _connectedNodesService;
    private readonly ISoundRepository _soundRepository;
    private readonly ISoundFileDeploymentService _soundDeploymentService;
    private readonly ILogger<ActivateSalonCommandHandler> _logger;

    public ActivateSalonCommandHandler(
        ISalonRepository repository,
        IActiveSessionTracker tracker,
        ISA818Repository sa818Repository,
        ISA818Service sa818Service,
        ISvxLinkConfigurationService configurationService,
        ISvxLinkDaemonService daemonService,
        IConnectedNodesService connectedNodesService,
        ISoundRepository soundRepository,
        ISoundFileDeploymentService soundDeploymentService,
        ILogger<ActivateSalonCommandHandler> logger)
    {
        _repository = repository;
        _tracker = tracker;
        _sa818Repository = sa818Repository;
        _sa818Service = sa818Service;
        _configurationService = configurationService;
        _daemonService = daemonService;
        _connectedNodesService = connectedNodesService;
        _soundRepository = soundRepository;
        _soundDeploymentService = soundDeploymentService;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        ActivateSalonCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activation du Salon {SalonId}", command.Id);

        var aggregateResult = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: errors => Validation<Error, Unit>.Fail(errors));

        var aggregate = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        if (aggregate.IsDeleted)
            return Error.Validation("SALON_DELETED", "Le salon est supprimé").ToFailure<Unit>();

        var currentActiveSalonId = _tracker.ActiveSalonId;
        if (currentActiveSalonId.HasValue && currentActiveSalonId.Value != command.Id)
        {
            _logger.LogInformation(
                "Auto-désactivation du salon actif {OldSalonId} avant activation de {NewSalonId}",
                currentActiveSalonId.Value, command.Id);

            var stopResult = await _daemonService.StopAsync(cancellationToken);
            if (stopResult.IsFail)
                return Error.Validation("SVXLINK_STOP_ERROR", "Impossible d'arrêter le daemon SVXLink").ToFailure<Unit>();

            _connectedNodesService.Reset();
            _tracker.SetActiveSalon(null);
        }

        var sa818Config = await _sa818Repository.GetConfigurationAsync(cancellationToken);
        if (sa818Config == null)
            return Error.Validation("SA818_CONFIG_NOT_FOUND", "Configuration SA818 introuvable").ToFailure<Unit>();

        _logger.LogInformation("Configuration du module SA818 pour le Salon {SalonName}", aggregate.Name);
        var commandSet = BuildSA818Commands(aggregate, sa818Config, _logger);
        var sa818Result = await _sa818Service.ConfigureAsync(commandSet, cancellationToken);
        if (sa818Result.IsFail)
            return Error.Validation("SA818_CONFIGURE_ERROR", "Impossible de configurer le module SA818").ToFailure<Unit>();

        // Déploiement du fichier son (optionnel — résilience si son supprimé)
        string? announceFilePath = null;
        var soundId = aggregate.Configuration.SoundId;
        if (soundId.HasValue)
        {
            _logger.LogInformation("Déploiement du son {SoundId} pour le Salon {SalonName}", soundId.Value, aggregate.Name);
            var soundResult = await _soundRepository.GetByIdAsync(soundId.Value, cancellationToken);
            await soundResult.Match(
                Succ: async sound =>
                {
                    if (sound.IsDeleted)
                    {
                        _logger.LogWarning(
                            "Le son {SoundId} est supprimé, activation continue sans annonce",
                            soundId.Value);
                        return;
                    }
                    var deployResult = await _soundDeploymentService.DeployAsync(sound, cancellationToken);
                    deployResult.Match(
                        Succ: path => { announceFilePath = path; return unit; },
                        Fail: errors =>
                        {
                            _logger.LogWarning(
                                "Échec du déploiement du son {SoundId}, activation continue sans annonce: {Errors}",
                                soundId.Value, errors);
                            return unit;
                        });
                },
                Fail: _ =>
                {
                    _logger.LogWarning(
                        "Son {SoundId} introuvable, activation continue sans annonce",
                        soundId.Value);
                    return Task.CompletedTask;
                });
        }
        else
        {
            // Pas de son configuré — nettoyer un éventuel fichier résiduel
            await _soundDeploymentService.CleanupAsync(cancellationToken);
        }

        _logger.LogInformation("Génération du fichier {Path}", SvxLinkConfPath);
        var configResult = await _configurationService.GenerateAsync(aggregate, SvxLinkConfPath, announceFilePath, cancellationToken);
        if (configResult.IsFail)
            return Error.Validation("SVXLINK_CONFIG_ERROR", "Impossible de générer le fichier svxlink.conf").ToFailure<Unit>();

        _logger.LogInformation("Redémarrage du daemon SVXLink");
        var daemonResult = await _daemonService.RestartAsync(cancellationToken);
        if (daemonResult.IsFail)
            return Error.Validation("SVXLINK_RESTART_ERROR", "Impossible de redémarrer le daemon SVXLink").ToFailure<Unit>();

        _tracker.SetActiveSalon(command.Id);

        _logger.LogInformation("Salon {SalonName} ({SalonId}) activé avec succès", aggregate.Name, command.Id);
        return unit.ToSuccess();
    }

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
