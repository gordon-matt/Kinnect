using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class ChatServiceTests
{
    [Fact]
    public async Task CreateRoomAsync_ReturnsConflict_WhenNameExists()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        db.Users.Add(InMemoryDb.CreateUser("admin", "admin", "a@test.com"));
        await db.SaveChangesAsync();

        db.ChatRooms.Add(new ChatRoom
        {
            Name = "General",
            AdminUserId = "admin",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var userInfo = new TestUserInfoService(new UserInfo("admin", "admin", "a@test.com"));
        var sut = new ChatService(
            new EntityFrameworkRepository<ChatRoom>(factory),
            new EntityFrameworkRepository<ChatMessage>(factory),
            new EntityFrameworkRepository<Person>(factory),
            userInfo);

        var result = await sut.CreateRoomAsync("General", "admin");

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task CreateRoomAsync_CreatesRoom()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        db.Users.Add(InMemoryDb.CreateUser("u1", "owner", "o@test.com"));
        await db.SaveChangesAsync();

        var userInfo = new TestUserInfoService(new UserInfo("u1", "owner", "o@test.com"));
        var sut = new ChatService(
            new EntityFrameworkRepository<ChatRoom>(factory),
            new EntityFrameworkRepository<ChatMessage>(factory),
            new EntityFrameworkRepository<Person>(factory),
            userInfo);

        var result = await sut.CreateRoomAsync("News", "u1");

        Assert.True(result.IsSuccess);
        Assert.Equal("News", result.Value.Name);
    }

    [Fact]
    public async Task CreatePrivateMessageAsync_StoresMessage()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        db.Users.AddRange(
            InMemoryDb.CreateUser("a", "alice", "a@test.com"),
            InMemoryDb.CreateUser("b", "bob", "b@test.com"));
        await db.SaveChangesAsync();

        var userInfo = new TestUserInfoService(
            new UserInfo("a", "alice", "a@test.com"),
            new UserInfo("b", "bob", "b@test.com"));
        var sut = new ChatService(
            new EntityFrameworkRepository<ChatRoom>(factory),
            new EntityFrameworkRepository<ChatMessage>(factory),
            new EntityFrameworkRepository<Person>(factory),
            userInfo);

        var result = await sut.CreatePrivateMessageAsync("a", "b", "Hello");

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello", result.Value.Content);
        await using var assertDb = InMemoryDb.CreateContext(options);
        Assert.Equal(1, await assertDb.ChatMessages.CountAsync());
    }

    [Fact]
    public async Task CreatePrivateMessageAsync_ReturnsInvalid_WhenEmpty()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        db.Users.AddRange(
            InMemoryDb.CreateUser("a", "a", "a@test.com"),
            InMemoryDb.CreateUser("b", "b", "b@test.com"));
        await db.SaveChangesAsync();

        var userInfo = new TestUserInfoService(
            new UserInfo("a", "a", "a@test.com"),
            new UserInfo("b", "b", "b@test.com"));
        var sut = new ChatService(
            new EntityFrameworkRepository<ChatRoom>(factory),
            new EntityFrameworkRepository<ChatMessage>(factory),
            new EntityFrameworkRepository<Person>(factory),
            userInfo);

        var result = await sut.CreatePrivateMessageAsync("a", "b", "   ");

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }
}