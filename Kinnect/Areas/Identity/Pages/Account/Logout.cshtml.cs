using Kinnect.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kinnect.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger) : PageModel
{
    public IActionResult OnGet()
    {
        if (signInManager.IsSignedIn(User))
        {
            return Page();
        }

        return RedirectToPage("./Login");
    }

    public async Task<IActionResult> OnPost()
    {
        await signInManager.SignOutAsync();
        logger.LogInformation("User logged out.");
        return RedirectToPage("./Login");
    }
}
