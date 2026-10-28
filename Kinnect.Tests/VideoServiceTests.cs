using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class VideoServiceTests
{
    [Fact]
    public async Task CreateAsync_WithTag_SyncsTags()
    {
        var (options, factory) = SqliteDb.Create();
        await using var db = SqliteDb.CreateContext(options);
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
            new EntityFrameworkRepository<VideoTag>(factory),
            new EntityFrameworkRepository<VideoEvent>(factory),
            new FileStorageService(new ConfigurationBuilder().Build(), new TestSettingsService()),
            NullLogger<VideoService>.Instance);

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
        var (options, factory) = SqliteDb.Create();
        var sut = new VideoService(
            new EntityFrameworkRepository<Video>(factory),
            new EntityFrameworkRepository<Tag>(factory),
            new EntityFrameworkRepository<VideoTag>(factory),
            new EntityFrameworkRepository<VideoEvent>(factory),
            new FileStorageService(new ConfigurationBuilder().Build(), new TestSettingsService()),
            NullLogger<VideoService>.Instance);

        var result = await sut.GetByIdAsync(99999);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheVideoAndItsFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "kinnect-video-delete-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = root })
                .Build();
            var storage = new FileStorageService(config, new TestSettingsService());
            string videoRelative = "user/videos/clip.mp4";
            string thumbRelative = "user/videos/thumbnails/clip.jpg";
            string videoFull = storage.GetFullPath(videoRelative);
            string thumbFull = storage.GetFullPath(thumbRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(videoFull)!);
            Directory.CreateDirectory(Path.GetDirectoryName(thumbFull)!);
            await File.WriteAllTextAsync(videoFull, "video");
            await File.WriteAllTextAsync(thumbFull, "thumb");

            var (options, factory) = SqliteDb.Create();
            await using var db = SqliteDb.CreateContext(options);
            var uploader = new Person
            {
                FamilyName = "V",
                GivenNames = "U",
                IsMale = true,
                UserId = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.People.Add(uploader);
            await db.SaveChangesAsync();

            var sut = new VideoService(
                new EntityFrameworkRepository<Video>(factory),
                new EntityFrameworkRepository<Tag>(factory),
                new EntityFrameworkRepository<VideoTag>(factory),
                new EntityFrameworkRepository<VideoEvent>(factory),
                storage,
                NullLogger<VideoService>.Instance);

            var created = await sut.CreateAsync(
                title: "Clip",
                description: null,
                filePath: videoRelative,
                thumbnailPath: thumbRelative,
                duration: TimeSpan.FromSeconds(3),
                uploadedByPersonId: uploader.Id,
                tags: null);

            var deleted = await sut.DeleteAsync(created.Value.Id, "owner", isAdmin: false);

            Assert.True(deleted.IsSuccess);
            Assert.False(File.Exists(videoFull));
            Assert.False(File.Exists(thumbFull));
            Assert.Equal(ResultStatus.NotFound, (await sut.GetByIdAsync(created.Value.Id)).Status);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeleteAsync_KeepsTheRowWhenTheFileIsLocked()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), "kinnect-video-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = root })
                .Build();
            var storage = new FileStorageService(config, new TestSettingsService());
            string videoRelative = "user/videos/clip.mp4";
            string videoFull = storage.GetFullPath(videoRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(videoFull)!);
            await File.WriteAllTextAsync(videoFull, "video");

            var (options, factory) = SqliteDb.Create();
            await using var db = SqliteDb.CreateContext(options);
            var uploader = new Person
            {
                FamilyName = "V",
                GivenNames = "U",
                IsMale = true,
                UserId = "owner",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.People.Add(uploader);
            await db.SaveChangesAsync();

            var sut = new VideoService(
                new EntityFrameworkRepository<Video>(factory),
                new EntityFrameworkRepository<Tag>(factory),
                new EntityFrameworkRepository<VideoTag>(factory),
                new EntityFrameworkRepository<VideoEvent>(factory),
                storage,
                NullLogger<VideoService>.Instance);

            var created = await sut.CreateAsync(
                title: "Clip",
                description: null,
                filePath: videoRelative,
                thumbnailPath: null,
                duration: TimeSpan.FromSeconds(3),
                uploadedByPersonId: uploader.Id,
                tags: null);

            await using var stream = new FileStream(videoFull, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var deleted = await sut.DeleteAsync(created.Value.Id, "owner", isAdmin: false);

            Assert.Equal(ResultStatus.Error, deleted.Status);
            Assert.True(File.Exists(videoFull));
            Assert.True((await sut.GetByIdAsync(created.Value.Id)).IsSuccess);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}