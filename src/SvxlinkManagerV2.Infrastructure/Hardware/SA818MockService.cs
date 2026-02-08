using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.Hardware;

/// <summary>
/// Implémentation mock du service SA818 pour l'environnement de développement.
/// Simule les appels au module SA818 sans interagir avec le hardware réel.
/// </summary>
public class SA818MockService : ISA818Service
{
    private readonly ILogger<SA818MockService> _logger;

    public SA818MockService(ILogger<SA818MockService> logger)
    {
        _logger = logger;
    }

    public Task<Validation<Error, Unit>> ConfigureAsync(SA818CommandSet commands, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Configuration du module SA818");
        _logger.LogInformation("MOCK: Commande DmoSetGroup: {DmoSetGroup}", commands.DmoSetGroup);
        _logger.LogInformation("MOCK: Commande DmoSetVolume: {DmoSetVolume}", commands.DmoSetVolume);
        _logger.LogInformation("MOCK: Commande SetFilter: {SetFilter}", commands.SetFilter);
        
        // Simuler un délai de traitement
        return Task.Delay(100, cancellationToken)
            .ContinueWith(_ =>
            {
                _logger.LogInformation("MOCK: Configuration du module SA818 terminée avec succès");
                return Validation<Error, Unit>.Success(Unit.Default);
            }, cancellationToken);
    }

    public Task<Validation<Error, bool>> IsConnectedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Vérification de la connexion du module SA818");
        
        // Le mock retourne toujours true (connecté)
        return Task.Delay(50, cancellationToken)
            .ContinueWith(_ =>
            {
                _logger.LogInformation("MOCK: Module SA818 connecté (simulé)");
                return Validation<Error, bool>.Success(true);
            }, cancellationToken);
    }
}
