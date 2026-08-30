using LanguageExt;
using MediatR;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Features.Audio.StartModulationTest;

/// <summary>
/// Commande diffusant une annonce vocale de test sur la fréquence.
///
/// Complément indispensable du test de porteuse : celui-ci ne prouve que la commande du PTT,
/// alors qu'une porteuse FM non modulée est inaudible. Seule l'émission d'un audio réel permet
/// de juger à l'oreille, sur un récepteur voisin, le réglage du niveau de sortie de la carte son.
/// </summary>
public record StartModulationTestCommand : IRequest<Validation<Error, Unit>>;

/// <summary>
/// Handler de <see cref="StartModulationTestCommand"/>.
///
/// L'audio est joué par SVXLink lui-même (TTS vers un WAV, puis commande DTMF interne 399
/// interceptée par Logic.tcl) : c'est donc SVXLink qui commande le PTT pendant la lecture, et
/// l'audio traverse le contrôle ALSA de restitution — exactement la chaîne que l'on veut régler.
/// </summary>
public class StartModulationTestCommandHandler
    : IRequestHandler<StartModulationTestCommand, Validation<Error, Unit>>
{
    /// <summary>
    /// Texte annoncé. Le comptage donne une durée suffisante pour juger le niveau, et des mots
    /// distincts rendent audible une distorsion qu'une tonalité pure masquerait.
    /// </summary>
    internal const string AnnouncementText =
        "Test audio. Un. Deux. Trois. Quatre. Cinq. Fin du test audio.";

    private readonly IVoiceAnnouncementService _announcementService;
    private readonly IPttTestService _pttTestService;
    private readonly IActiveSessionTracker _tracker;
    private readonly ISvxLinkDaemonService _daemonService;

    public StartModulationTestCommandHandler(
        IVoiceAnnouncementService announcementService,
        IPttTestService pttTestService,
        IActiveSessionTracker tracker,
        ISvxLinkDaemonService daemonService)
    {
        _announcementService = announcementService;
        _pttTestService = pttTestService;
        _tracker = tracker;
        _daemonService = daemonService;
    }

    public async Task<Validation<Error, Unit>> Handle(
        StartModulationTestCommand command,
        CancellationToken cancellationToken)
    {
        var blockedReason = await PttTestAvailability.GetBlockedReasonAsync(
            _tracker, _daemonService, cancellationToken);

        if (blockedReason is not null)
            return Error.Validation("MODULATION_TEST_UNAVAILABLE", blockedReason).ToFailure<Unit>();

        // Les deux tests se disputeraient le PTT : la porteuse est forcée sur le GPIO tandis que
        // SVXLink croit le piloter, et le minuteur de relâchement couperait l'annonce en cours.
        if (_pttTestService.State.IsTransmitting)
        {
            return Error.Conflict(
                    "Un test de porteuse est en cours : arrêtez-le avant de diffuser l'annonce de test.")
                .ToFailure<Unit>();
        }

        var result = await _announcementService.AnnounceAsync(AnnouncementText, cancellationToken);

        return result.Match(
            Succ: _ => Unit.Default.ToSuccess(),
            Fail: errors => Error.Validation(
                    "MODULATION_TEST_FAILED",
                    $"L'annonce de test n'a pas pu être diffusée : {string.Join(" ", errors.Select(error => error.Message))}")
                .ToFailure<Unit>());
    }
}
