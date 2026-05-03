namespace SvxlinkManagerV2.Domain.Common;

/// <summary>
/// Représente une erreur métier dans le domaine.
/// Utilisé avec LanguageExt Validation pour le Result Pattern.
/// Les erreurs métier ne sont pas des exceptions - elles représentent des échecs prévisibles.
/// </summary>
/// <param name="Code">Code unique identifiant le type d'erreur (ex: "INVALID_CALLSIGN")</param>
/// <param name="Message">Message descriptif de l'erreur destiné à l'utilisateur</param>
public record Error(string Code, string Message)
{
    /// <summary>
    /// Crée une erreur de validation
    /// </summary>
    /// <param name="code">Code de l'erreur</param>
    /// <param name="message">Message descriptif</param>
    /// <returns>Instance d'erreur</returns>
    public static Error Validation(string code, string message) => new(code, message);

    /// <summary>
    /// Crée une erreur "non trouvé"
    /// </summary>
    /// <param name="entityName">Nom de l'entité</param>
    /// <param name="id">Identifiant recherché</param>
    /// <returns>Instance d'erreur NotFound</returns>
    public static Error NotFound(string entityName, object id) =>
        new($"{entityName.ToUpper()}_NOT_FOUND", $"{entityName} with id '{id}' was not found.");

    /// <summary>
    /// Crée une erreur "conflit"
    /// </summary>
    /// <param name="message">Message descriptif du conflit</param>
    /// <returns>Instance d'erreur Conflict</returns>
    public static Error Conflict(string message) => new("CONFLICT", message);

    /// <summary>
    /// Retourne une représentation textuelle de l'erreur
    /// </summary>
    /// <returns>Chaîne formatée avec code et message</returns>
    public override string ToString() => $"[{Code}] {Message}";
}
