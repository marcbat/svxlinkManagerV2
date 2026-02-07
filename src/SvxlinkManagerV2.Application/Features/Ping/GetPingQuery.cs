namespace SvxlinkManagerV2.Application.Features.Ping;

/// <summary>
/// Query de test pour vérifier l'état du service Ping.
/// Convention : La Query et son Handler sont définis dans le même fichier pour améliorer la lisibilité.
/// </summary>
public record GetPingQuery();

/// <summary>
/// Handler pour la query GetPingQuery.
/// Wolverine découvre automatiquement les handlers via convention de nommage ou méthode Handle().
/// </summary>
public static class GetPingQueryHandler
{
    /// <summary>
    /// Traite la query GetPingQuery et retourne l'état du service.
    /// </summary>
    /// <param name="query">Query sans paramètres</param>
    /// <returns>Message indiquant que le service Ping est actif</returns>
    public static Task<string> Handle(GetPingQuery query)
    {
        return Task.FromResult("Ping service is alive");
    }
}
