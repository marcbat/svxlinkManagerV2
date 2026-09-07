using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Application.Features.Salons.SelectTalkGroup;

/// <summary>
/// Sélectionne un talkgroup à chaud sur le salon actif, sans régénérer <c>svxlink.conf</c>
/// ni redémarrer le daemon : la commande est composée dans le PTY DTMF, exactement comme
/// elle le serait depuis la radio.
/// </summary>
/// <param name="TalkGroup">
/// Talkgroup à rejoindre. <c>0</c> est une valeur légitime : SVXLink l'interprète comme
/// « aucun talkgroup ».
/// </param>
public record SelectTalkGroupCommand(int TalkGroup) : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler de <see cref="SelectTalkGroupCommand"/>.
/// </summary>
/// <remarks>
/// Le succès rapporté n'est que celui de <b>l'émission</b> de la commande : SVXLink peut
/// encore refuser le talkgroup. Le talkgroup réellement courant est publié par
/// <see cref="ITalkGroupStateService"/>, qui lit les logs — c'est aussi ce qui permet de
/// suivre un QSY décidé par le réflecteur, que l'application n'a pas demandé.
/// </remarks>
public class SelectTalkGroupCommandHandler
    : IRequestHandler<SelectTalkGroupCommand, Validation<Error, Unit>>
{
    private readonly ISalonRepository _repository;
    private readonly IActiveSessionTracker _sessionTracker;
    private readonly IDtmfPtyWriter _dtmfPtyWriter;
    private readonly ILogger<SelectTalkGroupCommandHandler> _logger;

    public SelectTalkGroupCommandHandler(
        ISalonRepository repository,
        IActiveSessionTracker sessionTracker,
        IDtmfPtyWriter dtmfPtyWriter,
        ILogger<SelectTalkGroupCommandHandler> logger)
    {
        _repository = repository;
        _sessionTracker = sessionTracker;
        _dtmfPtyWriter = dtmfPtyWriter;
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> Handle(
        SelectTalkGroupCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TalkGroup < 0)
            return Error.Validation("TALKGROUP_INVALID", "Le talkgroup doit être un entier positif").ToFailure<Unit>();

        var activeSalonId = _sessionTracker.ActiveSalonId;
        if (!activeSalonId.HasValue)
            return Error.Validation("TALKGROUP_NO_ACTIVE_SALON", "Aucun salon actif").ToFailure<Unit>();

        var aggregateResult = await _repository.GetByIdAsync(activeSalonId.Value, cancellationToken);
        if (aggregateResult.IsFail)
            return aggregateResult.Match(
                Succ: _ => throw new InvalidOperationException(),
                Fail: Validation<Error, Unit>.Fail);

        var salon = aggregateResult.Match(
            Succ: a => a,
            Fail: _ => throw new InvalidOperationException());

        // Un salon perroquet n'a pas de ReflectorLogic, et SVXLink 19.09.2 ne connaît pas
        // les talkgroups : dans les deux cas aucun préfixe de commande n'est déclaré et la
        // séquence se perdrait sans le moindre message d'erreur.
        if (salon.SalonType == SalonType.Parrot || salon.Configuration.ReflectorProtocol != ReflectorProtocol.V3)
            return Error.Validation(
                "TALKGROUP_NOT_SUPPORTED",
                "Le salon actif n'utilise pas le protocole V3 : les talkgroups ne s'y appliquent pas")
                .ToFailure<Unit>();

        var sequence = DtmfTalkGroupCommands.SelectTalkGroup(command.TalkGroup);
        _logger.LogInformation(
            "Sélection du talkgroup {TalkGroup} sur le salon {SalonName} via la séquence {Sequence}",
            command.TalkGroup, salon.Name, sequence);

        var writeResult = await _dtmfPtyWriter.SendCommandAsync(sequence, cancellationToken);

        return writeResult.Match(
            Succ: _ => unit.ToSuccess(),
            Fail: errors =>
            {
                _logger.LogError(
                    "Échec de l'envoi de la commande talkgroup : {Errors}",
                    string.Join(", ", errors.Select(e => e.Message)));

                return Error.Validation(
                    "TALKGROUP_COMMAND_FAILED",
                    "Impossible de transmettre la commande à SVXLink")
                    .ToFailure<Unit>();
            });
    }
}
