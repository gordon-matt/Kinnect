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
}