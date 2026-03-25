using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class PhotoServiceTests
{
    [Fact]
    public async Task CreateAsync_WithTag_CreatesTagAndPhoto()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var uploader = new Person
        {
            FamilyName = "U",
            GivenNames = "P",
            IsMale = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(uploader);
        await db.SaveChangesAsync();

        var sut = new PhotoService(
            new EntityFrameworkRepository<Photo>(factory),
            new EntityFrameworkRepository<Tag>(factory),
            new EntityFrameworkRepository<PhotoTag>(factory),
            new EntityFrameworkRepository<PersonPhoto>(factory));

        var result = await sut.CreateAsync(
            title: "Sunset",
            description: null,
            filePath: "photos/a.jpg",
            thumbnailPath: "thumbs/a.jpg",
            uploadedByPersonId: uploader.Id,
            tags: ["Nature"]);

        Assert.True(result.IsSuccess);
        Assert.Contains("Nature", result.Value.Tags);
        await using var assertDb = InMemoryDb.CreateContext(options);
        Assert.Equal(1, await assertDb.Tags.CountAsync(t => t.Name == "Nature"));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsForbidden_WhenLinkedUserMismatch()
    {
        var (options, factory) = InMemoryDb.Create();
        await using var db = InMemoryDb.CreateContext(options);
        var uploader = new Person
        {
            FamilyName = "A",
            GivenNames = "B",
            IsMale = true,
            UserId = "uid-1",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(uploader);
        await db.SaveChangesAsync();

        var photo = new Photo
        {
            Title = "x",
            FilePath = "p.jpg",
            UploadedByPersonId = uploader.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Photos.Add(photo);
        await db.SaveChangesAsync();

        var sut = new PhotoService(
            new EntityFrameworkRepository<Photo>(factory),
            new EntityFrameworkRepository<Tag>(factory),
            new EntityFrameworkRepository<PhotoTag>(factory),
            new EntityFrameworkRepository<PersonPhoto>(factory));

        var result = await sut.DeleteAsync(photo.Id, "other", isAdmin: false);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }
}