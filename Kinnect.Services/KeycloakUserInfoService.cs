using System.Text.RegularExpressions;
using NETCore.Keycloak.Client.HttpClients.Implementation;
using NETCore.Keycloak.Client.Models.Auth;
using NETCore.Keycloak.Client.Models.Users;

namespace Kinnect.Services;

/// <summary>
/// User info service backed by the Keycloak Admin REST API.
/// Used when <c>Authentication:Provider</c> is <c>Keycloak</c>.
/// Requires a service account with the <c>realm-management</c> → <c>view-users</c> role.
/// Configure admin credentials via <c>Authentication:Keycloak:AdminClientId</c> /
/// <c>Authentication:Keycloak:AdminClientSecret</c> (or falls back to the main
/// <c>ClientId</c> / <c>ClientSecret</c> if the main client has a service account).
/// </summary>
public class KeycloakUserInfoService(IConfiguration configuration, ILogger<KeycloakUserInfoService> logger) : IUserInfoService
{
    private record KeycloakAdminConfig(string BaseUrl, string Realm, string ClientId, string ClientSecret);

    public async Task<IReadOnlyList<UserInfo>> GetAllUsersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await FetchAllUsersAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "Failed to retrieve all Keycloak users");
            }

            return [];
        }
    }

    public async Task<IReadOnlyDictionary<string, UserInfo>> GetUserInfoAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToHashSet();
        if (ids.Count == 0)
        {
            return new Dictionary<string, UserInfo>();
        }

        try
        {
            var allUsers = await FetchAllUsersAsync(cancellationToken);
            return allUsers
                .Where(u => ids.Contains(u.UserId))
                .ToDictionary(u => u.UserId);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "Falling back to raw user IDs for Keycloak user info");
            }

            // Graceful fallback: return raw user IDs so subscriber lists still render
            return ids.ToDictionary(id => id, id => new UserInfo(id, id, string.Empty));
        }
    }

    private static string BuildDisplayName(KcUser user)
    {
        string fullName = $"{user.FirstName} {user.LastName}".Trim();
        return !string.IsNullOrEmpty(fullName) ? fullName : user.UserName ?? user.Id ?? string.Empty;
    }

    private static string ExtractBaseUrl(string authority)
    {
        int idx = authority.IndexOf("/realms/", StringComparison.Ordinal);
        return idx > 0 ? authority[..idx] : authority;
    }

    private static string ExtractRealm(string authority)
    {
        var match = Regex.Match(authority, @"/realms/([^/?#]+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private async Task<IReadOnlyList<UserInfo>> FetchAllUsersAsync(CancellationToken cancellationToken)
    {
        var config = GetAdminConfig();
        if (config is null)
        {
            return [];
        }

        string? token = await GetAdminTokenAsync(config, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return [];
        }

        try
        {
            var client = new KeycloakClient(config.BaseUrl);
            var response = await client.Users.ListUserAsync(
                config.Realm,
                token,
                new KcUserFilter { Max = 1000 },
                cancellationToken);

            if (response.IsError || response.Response is null)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("Failed to list Keycloak users: {Error}", response.ErrorMessage);
                }

                return [];
            }

            return response.Response
                .Where(u => u.Id is not null)
                .Select(u => new UserInfo(u.Id!, BuildDisplayName(u), u.Email ?? string.Empty))
                .ToList();
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "Exception listing Keycloak users");
            }

            return [];
        }
    }

    private KeycloakAdminConfig? GetAdminConfig()
    {
        string? authority = configuration["Authentication:Keycloak:Authority"];
        if (string.IsNullOrWhiteSpace(authority))
        {
            return null;
        }

        string baseUrl = ExtractBaseUrl(authority);
        string realm = ExtractRealm(authority);

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(realm))
        {
            return null;
        }

        string clientId = configuration["Authentication:Keycloak:AdminClientId"]
            ?? configuration["Authentication:Keycloak:ClientId"]
            ?? string.Empty;

        string clientSecret = configuration["Authentication:Keycloak:AdminClientSecret"]
            ?? configuration["Authentication:Keycloak:ClientSecret"]
            ?? string.Empty;

        return string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)
            ? null
            : new KeycloakAdminConfig(baseUrl, realm, clientId, clientSecret);
    }

    private async Task<string?> GetAdminTokenAsync(KeycloakAdminConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var client = new KeycloakClient(config.BaseUrl);
            var response = await client.Auth.GetClientCredentialsTokenAsync(
                config.Realm,
                new KcClientCredentials
                {
                    ClientId = config.ClientId,
                    Secret = config.ClientSecret
                },
                cancellationToken);

            if (response.IsError || response.Response is null)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("Failed to obtain Keycloak admin token: {Error}", response.ErrorMessage);
                }

                return null;
            }

            return response.Response.AccessToken;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "Exception obtaining Keycloak admin token");
            }

            return null;
        }
    }
}