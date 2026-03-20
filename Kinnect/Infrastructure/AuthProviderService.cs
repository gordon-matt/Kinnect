namespace Kinnect.Infrastructure;

public enum AuthProvider
{
    Identity,
    Keycloak
}

public interface IAuthProviderService
{
    AuthProvider Provider { get; }
    bool IsKeycloak { get; }
    bool IsIdentity { get; }
    string? KeycloakAccountUrl { get; }
}

public sealed class AuthProviderService : IAuthProviderService
{
    public AuthProvider Provider { get; }
    public bool IsKeycloak => Provider == AuthProvider.Keycloak;
    public bool IsIdentity => Provider == AuthProvider.Identity;
    public string? KeycloakAccountUrl { get; }

    public AuthProviderService(AuthProvider provider, string? keycloakAuthority)
    {
        Provider = provider;

        if (provider == AuthProvider.Keycloak && !string.IsNullOrWhiteSpace(keycloakAuthority))
        {
            KeycloakAccountUrl = keycloakAuthority.TrimEnd('/') + "/account";
        }
    }
}