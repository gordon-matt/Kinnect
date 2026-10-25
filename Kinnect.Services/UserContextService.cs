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
    AuthenticationStateProvider? authenticationState = null,
    IAccountIdResolver? accounts = null) : IUserContextService
{
    private bool resolved;
    private string? cachedId;

    public string? GetCurrentUserId()
    {
        if (resolved)
        {
            return cachedId;
        }

        resolved = true;
        cachedId = ResolveId();
        return cachedId;
    }

    public bool IsAdmin() => Principal()?.IsInRole(Constants.Roles.Administrator) == true;

    public bool IsEditor() =>
        IsAdmin() ||
        Principal()?.IsInRole(Constants.Roles.Editor) == true;

    public bool IsAuthenticated() => Principal()?.Identity?.IsAuthenticated == true;

    /// <summary>
    /// The circuit user wins when that claim belongs to an account. A circuit can also be authenticated
    /// with a claim that is not the account id, and <see cref="HttpContext"/> on a circuit can belong to
    /// some other request. Home and My profile look the person up by the account id, which is what the
    /// users page stores, so a claim that is not an account is skipped.
    /// </summary>
    private ClaimsPrincipal? Principal()
    {
        var circuitUser = CircuitUser();
        if (circuitUser?.Identity?.IsAuthenticated == true)
        {
            return circuitUser;
        }

        return httpContextAccessor.HttpContext?.User;
    }

    private string? ResolveId()
    {
        var circuitUser = CircuitUser();
        string? circuitId = AccountId(circuitUser);
        if (circuitUser?.Identity?.IsAuthenticated == true && circuitId is not null)
        {
            return circuitId;
        }

        var httpUser = httpContextAccessor.HttpContext?.User;
        string? httpId = AccountId(httpUser);
        if (httpUser?.Identity?.IsAuthenticated == true && httpId is not null)
        {
            return httpId;
        }

        return null;
    }

    /// <summary>
    /// Without a resolver (unit tests) the claim is used as-is. With one, only a real account id is returned,
    /// so an email or username claim still finds the same person the users page linked.
    /// </summary>
    private string? AccountId(ClaimsPrincipal? principal)
    {
        string? claim = UserId(principal);
        if (claim is null || accounts is null)
        {
            return claim;
        }

        try
        {
            return accounts.Resolve(claim);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return claim;
        }
    }

    private ClaimsPrincipal? CircuitUser()
    {
        if (authenticationState is null)
        {
            return null;
        }

        try
        {
            // Already completed on an established circuit. Reading it here keeps the circuit's
            // state; hopping to another thread can observe a provider that was never given a user.
            var pending = authenticationState.GetAuthenticationStateAsync();
            if (!pending.IsCompleted)
            {
                return null;
            }

            return pending.GetAwaiter().GetResult().User;
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
