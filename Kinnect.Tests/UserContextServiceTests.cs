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
    public void IsAuthenticated_TrueWhenIdentityIsAuthenticated()
    {
        var accessor = new HttpContextAccessor();
        var identity = new ClaimsIdentity(authenticationType: "Test");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var sut = new UserContextService(accessor);

        Assert.True(sut.IsAuthenticated());
    }
}