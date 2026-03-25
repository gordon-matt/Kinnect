using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class PostServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsPost_WhenAuthorExists()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var author = new Person
        {
            FamilyName = "Writer",
            GivenNames = "Pat",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(author);
        await db.SaveChangesAsync();

        var sut = new PostService(
            new EntityFrameworkRepository<Post>(factory),
            new EntityFrameworkRepository<Person>(factory));

        var result = await sut.CreateAsync(new PostCreateRequest { Content = "Hello" }, author.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello", result.Value.Content);
        await using var assertDb = InMemoryDb.CreateContext(options);
        Assert.Equal(1, await assertDb.Posts.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ReturnsNotFound_WhenAuthorMissing()
    {
        var (options, factory) = InMemoryDb.Create();
        var sut = new PostService(
            new EntityFrameworkRepository<Post>(factory),
            new EntityFrameworkRepository<Person>(factory));

        var result = await sut.CreateAsync(new PostCreateRequest { Content = "Hi" }, 999);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsForbidden_WhenNotAuthorUser()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var author = new Person
        {
            FamilyName = "A",
            GivenNames = "B",
            IsMale = true,
            UserId = "owner-id",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(author);
        await db.SaveChangesAsync();

        var post = new Post
        {
            AuthorPersonId = author.Id,
            Content = "x",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        var sut = new PostService(
            new EntityFrameworkRepository<Post>(factory),
            new EntityFrameworkRepository<Person>(factory));

        var result = await sut.DeleteAsync(post.Id, "other-id");

        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task GetByPersonAsync_ReturnsPostsNewestFirst()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var author = new Person
        {
            FamilyName = "A",
            GivenNames = "B",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(author);
        await db.SaveChangesAsync();

        var older = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.Posts.AddRange(
            new Post { AuthorPersonId = author.Id, Content = "old", CreatedAtUtc = older, UpdatedAtUtc = older },
            new Post { AuthorPersonId = author.Id, Content = "new", CreatedAtUtc = newer, UpdatedAtUtc = newer });
        await db.SaveChangesAsync();

        var sut = new PostService(
            new EntityFrameworkRepository<Post>(factory),
            new EntityFrameworkRepository<Person>(factory));

        var result = await sut.GetByPersonAsync(author.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(["new", "old"], result.Value.Select(p => p.Content).ToArray());
    }
}