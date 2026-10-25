using Microsoft.AspNetCore.Components.Authorization;

namespace Kinnect.Tests;

public class UserContextServiceTests
{
    [Fact]
    public void GetCurrentUserId_ReturnsNameIdentifierClaim()
    {
        var accessor = new HttpContextAccessor();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-123")],
            authenticationType: "Test");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var sut = new UserContextService(accessor);

        Assert.Equal("user-123", sut.GetCurrentUserId());
    }

    [Fact]
    public void IsAdmin_TrueWhenAdministratorRole()
    {
        var accessor = new HttpContextAccessor();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "admin"),
                new Claim(ClaimTypes.Role, Constants.Roles.Administrator)
            ],
            authenticationType: "Test");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var sut = new UserContextService(accessor);

        Assert.True(sut.IsAdmin());
    }

    [Fact]
    public void IsEditor_TrueWhenEditorRole()
    {
        var accessor = new HttpContextAccessor();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "editor"),
                new Claim(ClaimTypes.Role, Constants.Roles.Editor)
            ],
            authenticationType: "Test");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var sut = new UserContextService(accessor);

        Assert.True(sut.IsEditor());
    }

    [Fact]
    public void IsEditor_TrueWhenAdministratorRole()
    {
        var accessor = new HttpContextAccessor();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "admin"),
                new Claim(ClaimTypes.Role, Constants.Roles.Administrator)
            ],
            authenticationType: "Test");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var sut = new UserContextService(accessor);

        Assert.True(sut.IsEditor());
    }

    [Fact]
    public void IsEditor_FalseWhenUserRole()
    {
        var accessor = new HttpContextAccessor();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user"),
                new Claim(ClaimTypes.Role, Constants.Roles.User)
            ],
            authenticationType: "Test");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var sut = new UserContextService(accessor);

        Assert.False(sut.IsEditor());
    }

    [Fact]
    public void IsAuthenticated_TrueWhenIdentityIsAuthenticated()
    {
        var accessor = new HttpContextAccessor();
        var identity = new ClaimsIdentity(authenticationType: "Test");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var sut = new UserContextService(accessor);

        Assert.True(sut.IsAuthenticated());
    }

    [Fact]
    public void GetCurrentUserId_UsesCircuitWhenHttpContextIsAnonymous()
    {
        var accessor = new HttpContextAccessor();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "circuit-user")],
            authenticationType: "Test");

        var sut = new UserContextService(accessor, new StubAuthenticationState(new ClaimsPrincipal(identity)));

        Assert.Equal("circuit-user", sut.GetCurrentUserId());
    }

    [Fact]
    public void GetCurrentUserId_PrefersTheCircuitWhenHttpContextIsADifferentUser()
    {
        var accessor = new HttpContextAccessor();
        var httpIdentity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "wrong-request")],
            authenticationType: "Test");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(httpIdentity) };

        var circuitIdentity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "signed-in-user")],
            authenticationType: "Test");
        var sut = new UserContextService(accessor, new StubAuthenticationState(new ClaimsPrincipal(circuitIdentity)));

        Assert.Equal("signed-in-user", sut.GetCurrentUserId());
    }

    [Fact]
    public void GetCurrentUserId_AcceptsTheSubClaim()
    {
        var accessor = new HttpContextAccessor();
        var identity = new ClaimsIdentity(
            [new Claim("sub", "keycloak-user")],
            authenticationType: "Test");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var sut = new UserContextService(accessor);

        Assert.Equal("keycloak-user", sut.GetCurrentUserId());
    }

    private sealed class StubAuthenticationState(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }
}