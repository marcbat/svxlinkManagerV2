using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SvxlinkManagerV2.Presentation.Pages.Account;

/// <summary>
/// Endpoint de déconnexion. Utilise [IgnoreAntiforgeryToken] pour permettre la soumission
/// depuis un composant Blazor Server sans accès direct au générateur de tokens CSRF.
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class LogoutModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;

    public LogoutModel(SignInManager<IdentityUser> signInManager)
    {
        _signInManager = signInManager;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/login");
    }

    public IActionResult OnGet() => Redirect("/");
}
