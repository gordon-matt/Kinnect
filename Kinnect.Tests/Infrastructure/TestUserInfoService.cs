namespace Kinnect.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="IUserInfoService"/> for tests (no Moq).
/// </summary>
public sealed class TestUserInfoService : IUserInfoService
{
    private readonly Dictionary<string, UserInfo> _byId;

    public TestUserInfoService(params UserInfo[] users)
    {
        _byId = users.ToDictionary(u => u.UserId, StringComparer.Ordinal);
    }

    public Task<IReadOnlyList<UserInfo>> GetAllUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserInfo>>([.. _byId.Values.OrderBy(u => u.UserId)]);

    public Task<IReadOnlyDictionary<string, UserInfo>> GetUserInfoAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var wanted = userIds.ToHashSet(StringComparer.Ordinal);
        var dict = _byId
            .Where(kv => wanted.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        return Task.FromResult<IReadOnlyDictionary<string, UserInfo>>(dict);
    }
}