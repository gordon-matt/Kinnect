using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class AspNetIdentityUserInfoServiceTests
{
    [Fact]
    public async Task GetUserInfoAsync_ReturnsUsersFromIdentityTable()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        db.Users.Add(InMemoryDb.CreateUser("id-1", "alice", "alice@test.com"));
        await db.SaveChangesAsync();

        var sut = new AspNetIdentityUserInfoService(factory);

        var result = await sut.GetUserInfoAsync(["id-1"]);

        Assert.True(result.ContainsKey("id-1"));
        Assert.Equal("alice", result["id-1"].Username);
    }

    [Fact]
    public async Task GetAllUsersAsync_ReturnsOrderedByUserName()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        db.Users.Add(InMemoryDb.CreateUser("2", "zebra", "z@test.com"));
        db.Users.Add(InMemoryDb.CreateUser("1", "alpha", "a@test.com"));
        await db.SaveChangesAsync();

        var sut = new AspNetIdentityUserInfoService(factory);

        var list = await sut.GetAllUsersAsync();

        Assert.Equal(["alpha", "zebra"], list.Select(u => u.Username).ToArray());
    }
}