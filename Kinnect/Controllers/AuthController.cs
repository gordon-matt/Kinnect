using Kinnect.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Kinnect.Controllers;

public class AuthController(IAuthProviderService authProviderService) : Controller
{
    [HttpGet("auth/login")]
    public IActionResult Login(string? returnUrl = "/") => authProviderService.IsKeycloak
        ? Challenge(new AuthenticationProperties { RedirectUri = returnUrl },
            OpenIdConnectDefaults.AuthenticationScheme)
        : Redirect("/Identity/Account/Login" + (returnUrl != null ? $"?returnUrl={returnUrl}" : ""));

    [HttpPost("auth/logout")]
    public async Task<IActionResult> Logout()
    {
        if (authProviderService.IsKeycloak)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
            return Redirect("/");
        }

        return Redirect("/Identity/Account/Logout");
    }
}