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

    [Fact]
    public async Task CreateRoomMessageAsync_ReturnsForbidden_ForAnnouncements_WhenUserIsNotAdmin()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        db.Users.AddRange(
            InMemoryDb.CreateUser("admin", "admin", "admin@test.com"),
            InMemoryDb.CreateUser("u1", "user", "u1@test.com"));
        db.ChatRooms.Add(new ChatRoom
        {
            Name = "Announcements",
            AdminUserId = "admin",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var userInfo = new TestUserInfoService(
            new UserInfo("admin", "admin", "admin@test.com"),
            new UserInfo("u1", "user", "u1@test.com"));
        var sut = new ChatService(
            new EntityFrameworkRepository<ChatRoom>(factory),
            new EntityFrameworkRepository<ChatMessage>(factory),
            new EntityFrameworkRepository<Person>(factory),
            userInfo);

        int roomId = await db.ChatRooms.Where(r => r.Name == "Announcements").Select(r => r.Id).SingleAsync();
        var result = await sut.CreateRoomMessageAsync(roomId, "hello", "u1", isAdmin: false);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task DeleteRoomAsync_ReturnsForbidden_ForAnnouncements_EvenForAdmin()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        db.Users.Add(InMemoryDb.CreateUser("admin", "admin", "admin@test.com"));
        db.ChatRooms.Add(new ChatRoom
        {
            Name = "Announcements",
            AdminUserId = "admin",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var userInfo = new TestUserInfoService(new UserInfo("admin", "admin", "admin@test.com"));
        var sut = new ChatService(
            new EntityFrameworkRepository<ChatRoom>(factory),
            new EntityFrameworkRepository<ChatMessage>(factory),
            new EntityFrameworkRepository<Person>(factory),
            userInfo);

        int roomId = await db.ChatRooms.Where(r => r.Name == "Announcements").Select(r => r.Id).SingleAsync();
        var result = await sut.DeleteRoomAsync(roomId, "admin", isAdmin: true);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task GetRoomsAsync_ReturnsAnnouncementsFirst()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        db.Users.AddRange(
            InMemoryDb.CreateUser("admin", "admin", "admin@test.com"),
            InMemoryDb.CreateUser("u1", "user", "u1@test.com"));
        db.ChatRooms.AddRange(
            new ChatRoom { Name = "General", AdminUserId = "u1", CreatedAtUtc = DateTime.UtcNow },
            new ChatRoom { Name = "Announcements", AdminUserId = "admin", CreatedAtUtc = DateTime.UtcNow },
            new ChatRoom { Name = "Random", AdminUserId = "u1", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var userInfo = new TestUserInfoService(
            new UserInfo("admin", "admin", "admin@test.com"),
            new UserInfo("u1", "user", "u1@test.com"));
        var sut = new ChatService(
            new EntityFrameworkRepository<ChatRoom>(factory),
            new EntityFrameworkRepository<ChatMessage>(factory),
            new EntityFrameworkRepository<Person>(factory),
            userInfo);

        var result = await sut.GetRoomsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Announcements", result.Value.First().Name);
    }
}