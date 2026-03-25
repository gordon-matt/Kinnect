using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class TagServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsTagsSortedByName()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        db.Tags.AddRange(
            new Tag { Name = "Zebra" },
            new Tag { Name = "Alpha" });
        await db.SaveChangesAsync();

        var sut = new TagService(new EntityFrameworkRepository<Tag>(factory));

        var result = await sut.GetAllAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["Alpha", "Zebra"], result.Value.Select(t => t.Name).ToArray());
    }

    [Fact]
    public async Task SearchAsync_FiltersBySubstring()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        db.Tags.AddRange(new Tag { Name = "Family" }, new Tag { Name = "Travel" });
        await db.SaveChangesAsync();

        var sut = new TagService(new EntityFrameworkRepository<Tag>(factory));

        var result = await sut.SearchAsync("mil");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Family", result.Value.First().Name);
    }
}