using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Implémentation mock du service daemon SVXLink pour l'environnement de développement.
/// Simule les appels systemctl sans interagir avec le daemon réel.
/// </summary>
public class SvxLinkDaemonMockService : ISvxLinkDaemonService
{
    private readonly ILogger<SvxLinkDaemonMockService> _logger;

    public SvxLinkDaemonMockService(ILogger<SvxLinkDaemonMockService> logger)
    {
        _logger = logger;
    }

    public Task<Validation<Error, Unit>> RestartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Redémarrage du daemon SVXLink");
        _logger.LogInformation("MOCK: Exécution de la commande: systemctl restart svxlink");
        
        // Simuler un délai de traitement
        return Task.Delay(200, cancellationToken)
            .ContinueWith(_ =>
            {
                _logger.LogInformation("MOCK: Daemon SVXLink redémarré avec succès");
                return Validation<Error, Unit>.Success(Unit.Default);
            }, cancellationToken);
    }

    public Task<Validation<Error, bool>> IsRunningAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Vérification de l'état du daemon SVXLink");
        _logger.LogInformation("MOCK: Exécution de la commande: systemctl is-active svxlink");
        
        // Le mock retourne toujours true (daemon actif)
        return Task.Delay(50, cancellationToken)
            .ContinueWith(_ =>
            {
                _logger.LogInformation("MOCK: Daemon SVXLink actif (simulé)");
                return Validation<Error, bool>.Success(true);
            }, cancellationToken);
    }
}
