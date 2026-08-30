using LanguageExt;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Test d'émission manuel : maintient le PTT pendant une durée bornée, hors du contrôle de SVXLink.
///
/// Singleton : un seul test peut être en cours sur la machine, et son minuteur de relâchement
/// automatique doit survivre au circuit Blazor qui l'a déclenché — un utilisateur qui ferme son
/// navigateur ne doit pas laisser la station en émission.
/// </summary>
public interface IPttTestService
{
    /// <summary>
    /// État courant du test d'émission.
    /// </summary>
    PttTestState State { get; }

    /// <summary>
    /// Durée proposée par défaut pour un test d'émission, en secondes.
    /// </summary>
    int DefaultDurationSeconds { get; }

    /// <summary>
    /// Durée maximale admise pour un test d'émission, en secondes.
    /// </summary>
    int MaxDurationSeconds { get; }

    /// <summary>
    /// Émis à chaque changement d'état, y compris lors du relâchement automatique.
    /// </summary>
    event Action<PttTestState>? OnStateChanged;

    /// <summary>
    /// Passe en émission pour la durée demandée. Le relâchement est automatique à l'échéance.
    /// </summary>
    /// <param name="durationSeconds">Durée d'émission souhaitée, bornée par la configuration.</param>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, PttTestState>> StartAsync(int durationSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Relâche immédiatement le PTT. Sans effet si aucun test n'est en cours.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    Task<Validation<Error, PttTestState>> StopAsync(CancellationToken cancellationToken = default);
}
