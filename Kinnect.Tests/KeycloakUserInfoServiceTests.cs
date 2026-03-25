namespace Kinnect.Tests;

public class KeycloakUserInfoServiceTests
{
    [Fact]
    public async Task GetAllUsersAsync_WithoutKeycloakAuthority_ReturnsEmpty()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var sut = new KeycloakUserInfoService(config, NullLogger<KeycloakUserInfoService>.Instance);

        var users = await sut.GetAllUsersAsync();

        Assert.Empty(users);
    }

    [Fact]
    public async Task GetUserInfoAsync_WithoutKeycloakAuthority_ReturnsEmptyDictionary()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var sut = new KeycloakUserInfoService(config, NullLogger<KeycloakUserInfoService>.Instance);

        var map = await sut.GetUserInfoAsync(["any-id"]);

        Assert.Empty(map);
    }
}