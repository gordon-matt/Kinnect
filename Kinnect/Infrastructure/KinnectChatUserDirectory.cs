using System.Security.Claims;
using EasyChatz.Models;
using EasyChatz.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kinnect.Infrastructure;

/// <summary>
/// Names and roles for EasyChatz, taken from Kinnect accounts and the person linked to each one.
/// </summary>
public sealed class KinnectChatUserDirectory(
    IUserInfoService users,
    IPersonService people,
    IHttpContextAccessor httpContext,
    IServiceProvider services,
    IServiceScopeFactory scopes) : IChatUserDirectory
{
    public async Task<ChatUser?> FindAsync(string userId, CancellationToken cancellationToken = default)
    {
        var found = await users.GetUserInfoAsync([userId], cancellationToken);
        return found.TryGetValue(userId, out var info) ? await ToChatUserAsync(info) : null;
    }

    public async Task<IReadOnlyDictionary<string, ChatUser>> FindManyAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var found = await users.GetUserInfoAsync(userIds, cancellationToken);
        var result = new Dictionary<string, ChatUser>(StringComparer.Ordinal);
        foreach (var info in found.Values)
        {
            result[info.UserId] = await ToChatUserAsync(info);
        }

        return result;
    }

    private async Task<ChatUser> ToChatUserAsync(UserInfo info)
    {
        string name = info.Username;
        var person = await people.GetByUserIdAsync(info.UserId);
        if (person.IsSuccess && !string.IsNullOrWhiteSpace(person.Value.FullName))
        {
            name = person.Value.FullName;
        }

        (bool admin, bool editor) = await RolesAsync(info.UserId);
        return new ChatUser(info.UserId, name, admin, editor);
    }

    private async Task<(bool Admin, bool Editor)> RolesAsync(string userId)
    {
        var principal = await CurrentPrincipalAsync();
        if (principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value == userId)
        {
            bool admin = principal.IsInRole(Constants.Roles.Administrator);
            return (admin, admin || principal.IsInRole(Constants.Roles.Editor));
        }

        // A fresh scope keeps this off the circuit's Identity context. The messages page and the
        // unread badge both ask for roles, and those calls overlap on one Blazor circuit.
        await using var scope = scopes.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetService<UserManager<ApplicationUser>>();
        if (userManager is null)
        {
            return (false, false);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return (false, false);
        }

        bool isAdmin = await userManager.IsInRoleAsync(user, Constants.Roles.Administrator);
        return (isAdmin, isAdmin || await userManager.IsInRoleAsync(user, Constants.Roles.Editor));
    }

    private async Task<ClaimsPrincipal?> CurrentPrincipalAsync()
    {
        var principal = httpContext.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated == true)
        {
            return principal;
        }

        var authentication = services.GetService<AuthenticationStateProvider>();
        if (authentication is null)
        {
            return null;
        }

        try
        {
            return (await authentication.GetAuthenticationStateAsync()).User;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
