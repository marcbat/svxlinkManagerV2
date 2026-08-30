using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Application.Features.Audio;

/// <summary>
/// Conditions à réunir pour qu'un test d'émission soit possible.
///
/// Le PTT est une broche GPIO exportée et configurée en sortie par SVXLink au démarrage du
/// daemon : sans salon actif, la broche n'existe pas dans sysfs et le test ne pourrait rien
/// commander. Émettre sans que la chaîne radio soit montée n'aurait de toute façon pas de sens.
/// </summary>
internal static class PttTestAvailability
{
    /// <summary>
    /// Retourne le motif empêchant un test d'émission, ou null si le test est possible.
    /// </summary>
    /// <param name="tracker">Suivi du salon actif.</param>
    /// <param name="daemonService">État du processus svxlink.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    public static async Task<string?> GetBlockedReasonAsync(
        IActiveSessionTracker tracker,
        ISvxLinkDaemonService daemonService,
        CancellationToken cancellationToken)
    {
        if (!tracker.ActiveSalonId.HasValue)
            return "Aucun salon n'est actif : activez un salon avant de tester l'émission.";

        var isRunning = await daemonService.IsRunningAsync(cancellationToken);

        // Match ne peut pas rendre null : LanguageExt lève ResultIsNullException. Le cas « rien à
        // signaler » passe donc par la chaîne vide, retraduite en null juste après.
        var reason = isRunning.Match(
            Succ: running => running
                ? string.Empty
                : "Le daemon SVXLink n'est pas démarré : le PTT n'est pas disponible.",
            Fail: _ => "L'état du daemon SVXLink n'a pas pu être vérifié : le test d'émission est indisponible.");

        return reason.Length == 0 ? null : reason;
    }
}
