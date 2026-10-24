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
    public string? GetCurrentUserId() => Principal()?.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsAdmin() => Principal()?.IsInRole(Constants.Roles.Administrator) == true;

    public bool IsEditor() =>
        IsAdmin() ||
        Principal()?.IsInRole(Constants.Roles.Editor) == true;

    public bool IsAuthenticated() => Principal()?.Identity?.IsAuthenticated == true;

    private ClaimsPrincipal? Principal()
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
        {
            return httpUser;
        }

        if (authenticationState is null)
        {
            return httpUser;
        }

        try
        {
            // The provider's task is already completed on an established circuit. Hopping off
            // the renderer avoids blocking that sync context.
            var user = Task.Run(async () =>
                    (await authenticationState.GetAuthenticationStateAsync().ConfigureAwait(false)).User)
                .GetAwaiter()
                .GetResult();

            return user.Identity?.IsAuthenticated == true ? user : httpUser;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return httpUser;
        }
    }
}
