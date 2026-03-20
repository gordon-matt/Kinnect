namespace Kinnect.Services;

/// <summary>
/// Lightweight user profile returned by <see cref="IUserInfoService"/>.
/// </summary>
public record UserInfo(string UserId, string Username, string Email);

/// <summary>
/// Provider-agnostic service for looking up basic user information by ID.
/// Decouples the rest of the service layer from the concrete auth provider
/// (ASP.NET Identity vs Keycloak), since navigation properties to ApplicationUser
/// are not available on junction entities when Keycloak is active.
/// </summary>
public interface IUserInfoService
{
    /// <summary>
    /// Returns a dictionary keyed by user ID containing basic profile information
    /// for the given set of user IDs. Missing users are simply omitted.
    /// </summary>
    Task<IReadOnlyDictionary<string, UserInfo>> GetUserInfoAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all users known to the auth provider. Used by admin features
    /// (e.g. assigning channels to specific users).
    /// </summary>
    Task<IReadOnlyList<UserInfo>> GetAllUsersAsync(
        CancellationToken cancellationToken = default);
}