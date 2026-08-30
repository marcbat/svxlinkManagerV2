using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Features.Audio.GetAudioSettings;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Features.Audio.StopPttTest;

/// <summary>
/// Commande relâchant immédiatement le PTT d'un test d'émission en cours.
/// </summary>
public record StopPttTestCommand() : IRequest<Validation<Error, PttTestStatusDto>>;

/// <summary>
/// Handler de <see cref="StopPttTestCommand"/>.
/// Aucune condition préalable n'est vérifiée : relâcher le PTT doit toujours être possible,
/// y compris si le daemon SVXLink s'est arrêté entre-temps.
/// </summary>
public class StopPttTestCommandHandler
    : IRequestHandler<StopPttTestCommand, Validation<Error, PttTestStatusDto>>
{
    private readonly IPttTestService _pttTestService;
    private readonly IActiveSessionTracker _tracker;
    private readonly ISvxLinkDaemonService _daemonService;

    public StopPttTestCommandHandler(
        IPttTestService pttTestService,
        IActiveSessionTracker tracker,
        ISvxLinkDaemonService daemonService)
    {
        _pttTestService = pttTestService;
        _tracker = tracker;
        _daemonService = daemonService;
    }

    public async Task<Validation<Error, PttTestStatusDto>> Handle(
        StopPttTestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _pttTestService.StopAsync(cancellationToken);
        var blockedReason = await PttTestAvailability.GetBlockedReasonAsync(
            _tracker, _daemonService, cancellationToken);

        return result.Map(state => GetAudioSettingsQueryHandler.ToStatusDto(state, blockedReason));
    }
}
