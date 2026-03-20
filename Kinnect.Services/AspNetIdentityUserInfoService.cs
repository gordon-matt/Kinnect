using Kinnect.Data;

namespace Kinnect.Services;

/// <summary>
/// Resolves user info by querying the local ASP.NET Core Identity user table.
/// Used when <c>Authentication:Provider</c> is <c>Identity</c> (the default).
/// </summary>
public class AspNetIdentityUserInfoService(IDbContextFactory dbContextFactory) : IUserInfoService
{
    public async Task<IReadOnlyDictionary<string, UserInfo>> GetUserInfoAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.ToHashSet();
        if (ids.Count == 0)
        {
            return new Dictionary<string, UserInfo>();
        }

        using var context = (ApplicationDbContext)dbContextFactory.GetContext();
        var users = await context.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UserInfo(u.Id, u.UserName ?? u.Id, u.Email ?? string.Empty))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.UserId);
    }

    public async Task<IReadOnlyList<UserInfo>> GetAllUsersAsync(
        CancellationToken cancellationToken = default)
    {
        using var context = (ApplicationDbContext)dbContextFactory.GetContext();
        return await context.Users
            .OrderBy(u => u.UserName)
            .Select(u => new UserInfo(u.Id, u.UserName ?? u.Id, u.Email ?? string.Empty))
            .ToListAsync(cancellationToken);
    }
}