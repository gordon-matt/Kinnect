using Kinnect.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;

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
            // OIDC sign-out must run before clearing the cookie (id_token_hint) and must not be followed by Redirect(), which overwrites the Keycloak response.
            var oidcProps = new AuthenticationProperties { RedirectUri = "/" };
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, oidcProps);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return new EmptyResult();
        }

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        return Redirect("/");
    }
}