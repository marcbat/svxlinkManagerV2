using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Implémentation du service de gestion du compte utilisateur via ASP.NET Identity.
/// </summary>
public class UserAccountService : IUserAccountService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<UserAccountService> _logger;

    public UserAccountService(UserManager<IdentityUser> userManager, ILogger<UserAccountService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<bool> HasAnyUserAsync(CancellationToken cancellationToken = default)
    {
        return await _userManager.Users.AnyAsync(cancellationToken);
    }

    public async Task<Validation<Error, Unit>> CreateUserAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = new IdentityUser
            {
                UserName = username,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                _logger.LogInformation("Compte utilisateur créé avec succès pour {Username}", username);
                return Unit.Default;
            }

            var errors = result.Errors.Select(e => Error.New(e.Code == "PasswordTooShort" ? 400 : 422, $"USER_CREATE_FAILED: {e.Description}"));
            return Validation<Error, Unit>.Fail(Seq.createRange(errors));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création du compte utilisateur {Username}", username);
            return Error.New(500, "USER_CREATE_FAILED: Erreur inattendue lors de la création du compte");
        }
    }

    public async Task<Validation<Error, Unit>> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return Error.New(404, "USER_PASSWORD_CHANGE_FAILED: Utilisateur introuvable");
            }

            var isCorrect = await _userManager.CheckPasswordAsync(user, currentPassword);
            if (!isCorrect)
            {
                return Error.New(400, "USER_WRONG_CURRENT_PASSWORD: Mot de passe actuel incorrect");
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (result.Succeeded)
            {
                _logger.LogInformation("Mot de passe modifié avec succès pour l'utilisateur {UserId}", userId);
                return Unit.Default;
            }

            var errors = result.Errors.Select(e => Error.New(422, $"USER_PASSWORD_CHANGE_FAILED: {e.Description}"));
            return Validation<Error, Unit>.Fail(Seq.createRange(errors));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du changement de mot de passe pour l'utilisateur {UserId}", userId);
            return Error.New(500, "USER_PASSWORD_CHANGE_FAILED: Erreur inattendue lors du changement de mot de passe");
        }
    }
}
