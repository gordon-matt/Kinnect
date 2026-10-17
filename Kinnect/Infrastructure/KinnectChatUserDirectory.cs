using EasyChatz.Models;
using EasyChatz.Services;
using Microsoft.AspNetCore.Identity;

namespace Kinnect.Infrastructure;

/// <summary>
/// Names and roles for EasyChatz, taken from Kinnect accounts and the person linked to each one.
/// </summary>
public sealed class KinnectChatUserDirectory(
    IUserInfoService users,
    IPersonService people,
    IHttpContextAccessor httpContext,
    IServiceProvider services) : IChatUserDirectory
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

        bool admin = await IsInRoleAsync(info.UserId, Constants.Roles.Administrator);
        bool editor = admin || await IsInRoleAsync(info.UserId, Constants.Roles.Editor);
        return new ChatUser(info.UserId, name, admin, editor);
    }

    private async Task<bool> IsInRoleAsync(string userId, string role)
    {
        var principal = httpContext.HttpContext?.User;
        if (principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == userId
            && principal.IsInRole(role))
        {
            return true;
        }

        var userManager = services.GetService<UserManager<ApplicationUser>>();
        if (userManager is null)
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId);
        return user is not null && await userManager.IsInRoleAsync(user, role);
    }
}
