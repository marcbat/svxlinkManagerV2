namespace SvxlinkManagerV2.Application.Features.Ping;

/// <summary>
/// Commande de test pour valider le fonctionnement du mécanisme CQRS avec Wolverine.
/// Convention : La Command et son Handler sont définis dans le même fichier pour améliorer la lisibilité.
/// </summary>
/// <param name="Message">Message à envoyer avec la commande Ping</param>
public record PingCommand(string Message);

/// <summary>
/// Handler pour la commande PingCommand.
/// Wolverine découvre automatiquement les handlers via convention de nommage ou méthode Handle().
/// </summary>
public static class PingCommandHandler
{
    /// <summary>
    /// Traite la commande PingCommand et retourne une réponse.
    /// </summary>
    /// <param name="command">Commande Ping contenant le message</param>
    /// <returns>Réponse "Pong" avec le message d'origine</returns>
    public static Task<string> Handle(PingCommand command)
    {
        return Task.FromResult($"Pong: {command.Message}");
    }
}
