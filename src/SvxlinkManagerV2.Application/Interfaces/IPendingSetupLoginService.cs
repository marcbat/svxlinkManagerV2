namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de token à usage unique pour l'auto-login après création du compte dans le wizard.
/// Permet de contourner la contrainte WebSocket de Blazor Server (impossible d'écrire un cookie depuis un composant).
/// </summary>
public interface IPendingSetupLoginService
{
    /// <summary>
    /// Génère un token GUID à usage unique associé au nom d'utilisateur (TTL : 5 minutes).
    /// </summary>
    string GenerateToken(string username);

    /// <summary>
    /// Consomme le token (usage unique) et retourne le nom d'utilisateur associé.
    /// Retourne null si le token est invalide ou expiré.
    /// </summary>
    string? ConsumeToken(string token);
}
