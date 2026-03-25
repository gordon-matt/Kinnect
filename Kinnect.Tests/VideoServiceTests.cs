using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class VideoServiceTests
{
    [Fact]
    public async Task CreateAsync_WithTag_SyncsTags()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var uploader = new Person
        {
            FamilyName = "V",
            GivenNames = "U",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(uploader);
        await db.SaveChangesAsync();

        var sut = new VideoService(
            new EntityFrameworkRepository<Video>(factory),
            new EntityFrameworkRepository<Tag>(factory),
            new EntityFrameworkRepository<VideoTag>(factory));

        var result = await sut.CreateAsync(
            title: "Clip",
            description: null,
            filePath: "v.mp4",
            thumbnailPath: "t.jpg",
            duration: TimeSpan.FromSeconds(30),
            uploadedByPersonId: uploader.Id,
            tags: ["Trip"]);

        Assert.True(result.IsSuccess);
        Assert.Contains("Trip", result.Value.Tags);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFound_WhenMissing()
    {
        var (options, factory) = InMemoryDb.Create();
        var sut = new VideoService(
            new EntityFrameworkRepository<Video>(factory),
            new EntityFrameworkRepository<Tag>(factory),
            new EntityFrameworkRepository<VideoTag>(factory));

        var result = await sut.GetByIdAsync(99999);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}