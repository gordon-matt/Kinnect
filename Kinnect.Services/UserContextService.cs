using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kinnect.Services;

/// <summary>
/// The signed-in user. Interactive Blazor runs on a circuit where <see cref="HttpContext"/> is
/// null after the first render, so a click such as linking a person has to read the circuit.
/// </summary>
public class UserContextService(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider? authenticationState = null) : IUserContextService
{
    public string? GetCurrentUserId() => UserId(Principal());

    public bool IsAdmin() => Principal()?.IsInRole(Constants.Roles.Administrator) == true;

    public bool IsEditor() =>
        IsAdmin() ||
        Principal()?.IsInRole(Constants.Roles.Editor) == true;

    public bool IsAuthenticated() => Principal()?.Identity?.IsAuthenticated == true;

    /// <summary>
    /// The circuit user wins over <see cref="HttpContext"/>. On an interactive Blazor circuit the accessor can
    /// hold a different request than the one the page belongs to, so a link stored from the users page (the real
    /// account id) was invisible to Home and My profile, which read that other request.
    /// </summary>
    private ClaimsPrincipal? Principal()
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        var circuitUser = CircuitUser();
        if (circuitUser?.Identity?.IsAuthenticated == true)
        {
            return circuitUser;
        }

        return httpUser;
    }

    private ClaimsPrincipal? CircuitUser()
    {
        if (authenticationState is null)
        {
            return null;
        }

        try
        {
            // The provider's task is already completed on an established circuit. Hopping off
            // the renderer avoids blocking that sync context.
            return Task.Run(async () =>
                    (await authenticationState.GetAuthenticationStateAsync().ConfigureAwait(false)).User)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>Identity uses the long claim type. Keycloak leaves the id on <c>sub</c> when mapping is off.</summary>
    private static string? UserId(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        foreach (string type in new[] { ClaimTypes.NameIdentifier, "sub", "nameid" })
        {
            string? value = principal.FindFirst(type)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
