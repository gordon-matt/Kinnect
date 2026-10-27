using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kinnect.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger) : PageModel
{
    public IActionResult OnGet() => signInManager.IsSignedIn(User) ? Page() : RedirectToPage("./Login");

    public async Task<IActionResult> OnPost()
    {
        await signInManager.SignOutAsync();
        logger.LogInformation("User logged out.");
        return RedirectToPage("./Login");
    }
}
