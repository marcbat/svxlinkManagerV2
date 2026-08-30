using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Features.Audio.GetAudioSettings;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Audio.StartPttTest;

/// <summary>
/// Commande déclenchant un test d'émission d'une durée bornée.
/// </summary>
/// <param name="DurationSeconds">Durée d'émission souhaitée en secondes.</param>
public record StartPttTestCommand(int DurationSeconds) : IRequest<Validation<Error, PttTestStatusDto>>;

/// <summary>
/// Handler de <see cref="StartPttTestCommand"/>.
/// Refuse le test tant qu'aucun salon n'est actif : c'est SVXLink qui exporte et configure la
/// broche PTT, et émettre hors chaîne radio montée n'aurait pas de sens.
/// </summary>
public class StartPttTestCommandHandler
    : IRequestHandler<StartPttTestCommand, Validation<Error, PttTestStatusDto>>
{
    private readonly IPttTestService _pttTestService;
    private readonly IActiveSessionTracker _tracker;
    private readonly ISvxLinkDaemonService _daemonService;

    public StartPttTestCommandHandler(
        IPttTestService pttTestService,
        IActiveSessionTracker tracker,
        ISvxLinkDaemonService daemonService)
    {
        _pttTestService = pttTestService;
        _tracker = tracker;
        _daemonService = daemonService;
    }

    public async Task<Validation<Error, PttTestStatusDto>> Handle(
        StartPttTestCommand command,
        CancellationToken cancellationToken)
    {
        var blockedReason = await PttTestAvailability.GetBlockedReasonAsync(
            _tracker, _daemonService, cancellationToken);

        if (blockedReason is not null)
            return Error.Validation("PTT_TEST_UNAVAILABLE", blockedReason).ToFailure<PttTestStatusDto>();

        var result = await _pttTestService.StartAsync(command.DurationSeconds, cancellationToken);

        return result.Map(state => GetAudioSettingsQueryHandler.ToStatusDto(state, blockedReason: null));
    }
}
