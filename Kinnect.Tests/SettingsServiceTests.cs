using Kinnect.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Kinnect.Tests;

public class SettingsServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsDefaultsWhenNothingIsSavedOrConfigured()
    {
        var (sut, _) = Create();

        var tree = await sut.GetAsync<TreeSettings>();
        var images = await sut.GetAsync<ImageSettings>();

        Assert.Equal(TreeSettings.DefaultMaxGenerations, tree.MaxGenerations);
        Assert.Equal(1920, images.MaxWidth);
    }

    [Fact]
    public async Task GetAsync_ImportsConfigurationIntoTheDatabaseWhenTheDatabaseHasNone()
    {
        var (sut, options) = Create(new Dictionary<string, string?>
        {
            ["ImageProcessing:MaxWidth"] = "1000",
            ["ImageProcessing:Quality"] = "55",
        });

        var images = await sut.GetAsync<ImageSettings>();

        Assert.Equal(1000, images.MaxWidth);
        Assert.Equal(55, images.Quality);
        Assert.Equal(1080, images.MaxHeight);

        await using var db = SqliteDb.CreateContext(options);
        var stored = Assert.Single(db.ImageSettings);
        Assert.Equal(1000, stored.MaxWidth);
    }

    [Fact]
    public async Task GetAsync_PrefersTheDatabaseOverConfiguration()
    {
        var (sut, options) = Create(new Dictionary<string, string?> { ["ImageProcessing:MaxWidth"] = "1000" });
        await using (var db = SqliteDb.CreateContext(options))
        {
            db.ImageSettings.Add(new ImageSettings { MaxWidth = 800 });
            await db.SaveChangesAsync();
        }

        var images = await sut.GetAsync<ImageSettings>();

        Assert.Equal(800, images.MaxWidth);
        await using var assertDb = SqliteDb.CreateContext(options);
        Assert.Equal(800, Assert.Single(assertDb.ImageSettings).MaxWidth);
    }

    [Fact]
    public async Task GetAsync_ReadsSmtpSectionIntoEmailSettings()
    {
        var (sut, _) = Create(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "mail.example.com",
            ["Smtp:Port"] = "587",
            ["Smtp:EnableSsl"] = "true",
            ["Smtp:FromAddress"] = "noreply@example.com",
        });

        var email = await sut.GetAsync<EmailSettings>();

        Assert.Equal("mail.example.com", email.Host);
        Assert.Equal(587, email.Port);
        Assert.True(email.EnableSsl);
        Assert.True(email.IsConfigured);
    }

    [Fact]
    public async Task SaveAsync_UpdatesTheSingleRowAndTheNextReadSeesIt()
    {
        var (sut, options) = Create();
        var tree = await sut.GetAsync<TreeSettings>();
        tree.MaxGenerations = 5;
        Assert.True((await sut.SaveAsync(tree)).IsSuccess);

        var again = await sut.GetAsync<TreeSettings>();
        again.MaxGenerations = 6;
        Assert.True((await sut.SaveAsync(again)).IsSuccess);

        Assert.Equal(6, (await sut.GetAsync<TreeSettings>()).MaxGenerations);
        await using var db = SqliteDb.CreateContext(options);
        Assert.Equal(6, Assert.Single(db.TreeSettings).MaxGenerations);
    }

    [Fact]
    public async Task GetAsync_ReturnsACopyThatDoesNotChangeTheCachedSettings()
    {
        var (sut, _) = Create();
        var first = await sut.GetAsync<TreeSettings>();
        first.MaxGenerations = 3;

        var second = await sut.GetAsync<TreeSettings>();

        Assert.Equal(TreeSettings.DefaultMaxGenerations, second.MaxGenerations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(TreeSettings.LargestMaxGenerations + 1)]
    public async Task SaveAsync_RejectsGenerationLimitsOutOfRange(int maxGenerations)
    {
        var (sut, _) = Create();

        var result = await sut.SaveAsync(new TreeSettings { MaxGenerations = maxGenerations });

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task SaveAsync_NormalisesDocumentExtensionsAndRejectsUnknownVideoSizes()
    {
        var (sut, _) = Create();

        var documents = await sut.SaveAsync(new DocumentSettings { AllowedExtensions = " PDF, .Jpg ,,png " });
        var video = await sut.SaveAsync(new VideoSettings { OutputVideoSize = "NotASize" });

        Assert.True(documents.IsSuccess);
        Assert.Equal(".pdf,.jpg,.png", documents.Value.AllowedExtensions);
        Assert.Equal(ResultStatus.Invalid, video.Status);
    }

    private static (SettingsService Sut, DbContextOptions<ApplicationDbContext> Options) Create(
        Dictionary<string, string?>? configuration = null)
    {
        var (options, factory) = SqliteDb.Create();
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory>(factory);
        services.AddScoped(typeof(IRepository<>), typeof(EntityFrameworkRepository<>));
        var provider = services.BuildServiceProvider();

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configuration ?? [])
            .Build();
        return (new SettingsService(provider.GetRequiredService<IServiceScopeFactory>(), config, NullLogger<SettingsService>.Instance), options);
    }
}
