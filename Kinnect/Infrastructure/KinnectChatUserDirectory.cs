using System.Security.Claims;
using EasyChatz.Models;
using EasyChatz.Services;
using Kinnect.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kinnect.Infrastructure;

/// <summary>
/// Names and roles for EasyChatz, taken from Kinnect accounts and the person linked to each one.
/// </summary>
public sealed class KinnectChatUserDirectory(
    IUserInfoService users,
    IHttpContextAccessor httpContext,
    IServiceProvider services,
    IServiceScopeFactory scopes) : IChatUserDirectory
{
    public async Task<ChatUser?> FindAsync(string userId, CancellationToken cancellationToken = default)
    {
        var found = await users.GetUserInfoAsync([userId], cancellationToken);
        if (!found.TryGetValue(userId, out var info))
        {
            return null;
        }

        var names = await PersonNamesAsync([info.UserId], cancellationToken);
        return await ToChatUserAsync(info, names);
    }

    public async Task<IReadOnlyDictionary<string, ChatUser>> FindManyAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var found = await users.GetUserInfoAsync(userIds, cancellationToken);
        var names = await PersonNamesAsync(found.Keys, cancellationToken);
        var result = new Dictionary<string, ChatUser>(StringComparer.Ordinal);
        foreach (var info in found.Values)
        {
            result[info.UserId] = await ToChatUserAsync(info, names);
        }

        return result;
    }

    private async Task<ChatUser> ToChatUserAsync(UserInfo info, IReadOnlyDictionary<string, string> names)
    {
        string name = names.TryGetValue(info.UserId, out string? personName) && !string.IsNullOrWhiteSpace(personName)
            ? personName
            : info.Username;

        (bool admin, bool editor) = await RolesAsync(info.UserId);
        return new ChatUser(info.UserId, name, admin, editor);
    }

    /// <summary>
    /// Given and family name of the person linked to each account. A fresh scope keeps this off
    /// the circuit context the messages page and the unread badge already share.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> PersonNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        string[] ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        await using var scope = scopes.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory>();
        using var context = (ApplicationDbContextBase)factory.GetContext();
        var rows = await context.People.AsNoTracking()
            .Where(person => person.UserId != null && ids.Contains(person.UserId))
            .Select(person => new { person.UserId, person.GivenNames, person.FamilyName })
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.UserId))
            .GroupBy(row => row.UserId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => PersonName(group.First().GivenNames, group.First().FamilyName),
                StringComparer.Ordinal);
    }

    private static string PersonName(string? givenNames, string? familyName) =>
        string.Join(' ', new[] { givenNames, familyName }.Where(part => !string.IsNullOrWhiteSpace(part)));

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
