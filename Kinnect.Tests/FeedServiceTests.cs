using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class FeedServiceTests
{
    [Fact]
    public async Task GetFeedAsync_OrdersByCreatedAtDescending()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var person = new Person
        {
            FamilyName = "P",
            GivenNames = "Q",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var t1 = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        db.Posts.Add(new Post
        {
            AuthorPersonId = person.Id,
            Content = "post",
            CreatedAtUtc = t1,
            UpdatedAtUtc = t1
        });
        db.Photos.Add(new Photo
        {
            Title = "photo",
            FilePath = "/p.jpg",
            UploadedByPersonId = person.Id,
            CreatedAtUtc = t2
        });
        db.Videos.Add(new Video
        {
            Title = "video",
            FilePath = "/v.mp4",
            UploadedByPersonId = person.Id,
            CreatedAtUtc = t3
        });
        await db.SaveChangesAsync();

        var sut = new FeedService(
            new EntityFrameworkRepository<Post>(factory),
            new EntityFrameworkRepository<Photo>(factory),
            new EntityFrameworkRepository<Video>(factory));

        var result = await sut.GetFeedAsync(page: 1, pageSize: 10);

        Assert.True(result.IsSuccess);
        var types = result.Value.Select(f => f.Type).ToArray();
        Assert.Equal(["photo", "post", "video"], types);
    }
}