using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Presentation.Pages;

/// <summary>
/// Page de connexion. Doit être une Razor Page pour pouvoir écrire le cookie HTTP depuis le serveur.
/// </summary>
[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IUserAccountService _userAccountService;

    public LoginModel(SignInManager<IdentityUser> signInManager, IUserAccountService userAccountService)
    {
        _signInManager = signInManager;
        _userAccountService = userAccountService;
    }

    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";

        // Si aucun compte n'existe, rediriger directement vers le wizard de configuration
        var hasAnyUser = await _userAccountService.HasAnyUserAsync();
        if (!hasAnyUser)
            return Redirect("/setup");

        // Nettoyer les cookies externes existants
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? username, string? password, bool rememberMe = false, string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Veuillez saisir un nom d'utilisateur et un mot de passe.";
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(username, password, rememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // LocalRedirect refuse une URL absolue : protège contre l'open redirect
            // via un returnUrl forgé.
            return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
        }

        if (result.IsLockedOut)
        {
            ErrorMessage = "Compte temporairement verrouillé après plusieurs tentatives infructueuses. Réessayez dans quelques minutes.";
            return Page();
        }

        ErrorMessage = "Nom d'utilisateur ou mot de passe incorrect.";
        return Page();
    }
}
