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
    public bool HasAnyUser { get; set; }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";
        HasAnyUser = await _userAccountService.HasAnyUserAsync();

        // Nettoyer les cookies externes existants
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? username, string? password, bool rememberMe = false, string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";
        HasAnyUser = await _userAccountService.HasAnyUserAsync();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Veuillez saisir un nom d'utilisateur et un mot de passe.";
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(username, password, rememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl ?? "/");
        }

        ErrorMessage = "Nom d'utilisateur ou mot de passe incorrect.";
        return Page();
    }
}
