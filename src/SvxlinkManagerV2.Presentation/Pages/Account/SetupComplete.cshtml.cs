using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Presentation.Pages.Account;

/// <summary>
/// Endpoint GET d'auto-login post-wizard de création de compte.
/// Consomme le token à usage unique, connecte l'utilisateur et redirige vers /setup/callsign.
/// </summary>
[AllowAnonymous]
public class SetupCompleteModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IPendingSetupLoginService _pendingSetupLoginService;

    public SetupCompleteModel(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IPendingSetupLoginService pendingSetupLoginService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _pendingSetupLoginService = pendingSetupLoginService;
    }

    public async Task<IActionResult> OnGetAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Redirect("/login");

        var username = _pendingSetupLoginService.ConsumeToken(token);
        if (username is null)
            return Redirect("/login");

        var user = await _userManager.FindByNameAsync(username);
        if (user is null)
            return Redirect("/login");

        await _signInManager.SignInAsync(user, isPersistent: false);
        return Redirect("/setup/callsign");
    }
}
