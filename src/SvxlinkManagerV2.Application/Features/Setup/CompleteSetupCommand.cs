using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Entities;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.Setup;

/// <summary>
/// Commande finalisant le wizard de configuration initiale.
/// Crée les 6 salons avec les callsigns et fréquences saisis par l'utilisateur,
/// crée la configuration générale, puis invalide le cache du statut de setup.
/// </summary>
public record CompleteSetupCommand(SetupData Data) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler pour <see cref="CompleteSetupCommand"/>.
/// </summary>
public class CompleteSetupCommandHandler
    : IRequestHandler<CompleteSetupCommand, Validation<Error, Unit>>
{
    private readonly ISalonRepository _salonRepository;
    private readonly IGeneralConfigurationRepository _generalConfigRepository;
    private readonly ISetupStatusService _setupStatusService;
    private readonly ILogger<CompleteSetupCommandHandler> _logger;

    public CompleteSetupCommandHandler(
        ISalonRepository salonRepository,
        IGeneralConfigurationRepository generalConfigRepository,
        ISetupStatusService setupStatusService,
        ILogger<CompleteSetupCommandHandler> logger)
    {
        _salonRepository = salonRepository;
        _generalConfigRepository = generalConfigRepository;
        _setupStatusService = setupStatusService;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        CompleteSetupCommand command,
        CancellationToken cancellationToken)
    {
        var data = command.Data;
        var errors = new List<Error>();

        // 1. Seed des 6 salons avec les valeurs saisies par l'utilisateur
        foreach (var (id, name, host, port, authKey, dtmfCode) in GetOriginalSalons())
        {
            var configuration = new SvxLinkConfiguration(
                Id: Guid.NewGuid(),
                Logics: "SimplexLogic,ReflectorLogic",
                CfgDir: "svxlink.d",
                CardSampleRate: 16000,
                CardChannels: 1,
                Host: host,
                Port: port,
                Callsign: data.Callsign,
                AuthKey: authKey,
                JitterBufferDelay: 0,
                ReflectorProtocol: Domain.Aggregates.Salon.Enums.ReflectorProtocol.V2,
                CertEmail: null,
                SimplexCallsign: data.SimplexCallsign,
                Modules: "ModuleHelp",
                ShortIdentInterval: 600,
                LongIdentInterval: 3600,
                ReportCtcss: null,
                DefaultLang: "fr_FR",
                RgrSoundDelay: 0,
                RxFrequency: data.RxFrequency,
                TxFrequency: data.TxFrequency,
                RxCtcss: data.RxCtcss,
                TxCtcss: data.TxCtcss);

            var createResult = SalonAggregate.Create(
                id: id,
                name: name,
                isDefault: false,
                isTemporized: false,
                configuration: configuration);

            await createResult.Match(
                async aggregate =>
                {
                    if (dtmfCode.HasValue)
                        aggregate.UpdateDtmfCode(dtmfCode.Value);

                    var saveResult = await _salonRepository.SaveAsync(aggregate, cancellationToken);
                    saveResult.IfFail(errs =>
                    {
                        errors.AddRange(errs);
                        _logger.LogError(
                            "CompleteSetupCommand: Erreur sauvegarde salon '{Name}': {Errors}",
                            name,
                            string.Join(", ", errs.Select(e => e.Message)));
                    });
                },
                errs =>
                {
                    errors.AddRange(errs);
                    _logger.LogError(
                        "CompleteSetupCommand: Erreur création salon '{Name}': {Errors}",
                        name,
                        string.Join(", ", errs.Select(e => e.Message)));
                    return Task.CompletedTask;
                });
        }

        if (errors.Count > 0)
            return errors.ToSeq();

        // 2. Création de la configuration générale avec les fréquences du wizard
        var existingConfig = await _generalConfigRepository.GetAsync(cancellationToken);

        Validation<Error, Unit> configResult;
        if (existingConfig is null)
        {
            var createConfig = GeneralConfigurationAggregate.Create(
                startReflectorOnStartup: false,
                startDefaultSalonOnStartup: false,
                defaultRxFrequency: data.RxFrequency,
                defaultTxFrequency: data.TxFrequency);

            configResult = await createConfig.MatchAsync(
                async aggregate => await _generalConfigRepository.SaveAsync(aggregate, cancellationToken),
                errs => Task.FromResult<Validation<Error, Unit>>(errs));
        }
        else
        {
            var updateResult = existingConfig.Update(
                startReflectorOnStartup: existingConfig.StartReflectorOnStartup,
                startDefaultSalonOnStartup: existingConfig.StartDefaultSalonOnStartup,
                defaultRxFrequency: data.RxFrequency,
                defaultTxFrequency: data.TxFrequency);

            configResult = await updateResult.MatchAsync(
                async _ => await _generalConfigRepository.SaveAsync(existingConfig, cancellationToken),
                errs => Task.FromResult<Validation<Error, Unit>>(errs));
        }

        if (configResult.IsFail)
            return configResult;

        // 3. Invalidation du cache (le setup est terminé)
        _setupStatusService.InvalidateCache();

        _logger.LogInformation(
            "CompleteSetupCommand: wizard terminé — Callsign={Callsign}, SimplexCallsign={SimplexCallsign}, " +
            "RxFreq={RxFreq}, TxFreq={TxFreq}",
            data.Callsign, data.SimplexCallsign, data.RxFrequency, data.TxFrequency);

        return LanguageExt.Prelude.unit;
    }

    /// <summary>
    /// Retourne les 6 salons originaux avec leurs GUIDs fixes (compatibilité migration legacy).
    /// </summary>
    private static IEnumerable<(Guid Id, string Name, string Host, int Port, string AuthKey, int? DtmfCode)> GetOriginalSalons()
    {
        yield return (new Guid("235a4521-15a1-4e02-a540-91ee600452ac"), "Réseau des Répéteurs Francophones", "rrf2.f5nlg.ovh", 5300, "Magnifique123456789!", 96);
        yield return (new Guid("1f2e87b8-d984-4c05-8a4a-ffad65c829a9"), "Salon Suisse Romand", "salonsuisseromand.hbspot.ch", 5300, "xD9wW5gO7yD9hN5o", 200);
        yield return (new Guid("0f669a03-dcf1-4277-9b07-54f6a0fd3037"), "French Open Network", "serveur.f1tzo.com", 5300, "FON-F1TZO", 97);
        yield return (new Guid("a749ffe5-16c7-45da-809d-c048908f115c"), "Salon Technique", "rrf3.f5nlg.ovh", 5301, "Magnifique123456789!", 98);
        yield return (new Guid("d4c59d86-947c-4b1d-831a-807c1877d426"), "Salon Bavardage", "serveur.f1tzo.com", 5301, "FON-F1TZO", 100);
        yield return (new Guid("9f99b18b-96ea-453d-b07a-7923c09c939f"), "Salon Local", "serveur.f1tzo.com", 5302, "FON-F1TZO", 101);
    }
}
