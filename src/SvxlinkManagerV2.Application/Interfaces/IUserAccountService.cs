using LanguageExt;
using LanguageExt.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de gestion du compte utilisateur unique de l'application.
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// Vérifie si au moins un utilisateur existe dans la base de données.
    /// </summary>
    Task<bool> HasAnyUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crée le compte utilisateur unique de l'application.
    /// </summary>
    /// <param name="username">Nom d'utilisateur</param>
    /// <param name="password">Mot de passe (min 6 caractères)</param>
    Task<Validation<Error, Unit>> CreateUserAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifie le mot de passe de l'utilisateur.
    /// </summary>
    /// <param name="userId">Identifiant de l'utilisateur</param>
    /// <param name="currentPassword">Mot de passe actuel</param>
    /// <param name="newPassword">Nouveau mot de passe</param>
    Task<Validation<Error, Unit>> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
