namespace Kinnect.Tests;

public class FileStorageServiceTests : IDisposable
{
    private readonly string _basePath;

    public FileStorageServiceTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "kinnect-fs-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_basePath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_basePath))
            {
                Directory.Delete(_basePath, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup for temp tests
        }
    }

    [Fact]
    public async Task SaveFileAsync_PersistsFileAndReturnsRelativePath()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _basePath })
            .Build();
        var sut = new FileStorageService(config, Options.Create(new ImageProcessingOptions()));

        await using var stream = new MemoryStream("hello"u8.ToArray());
        string relative = await sut.SaveFileAsync(stream, "docs", "note.txt", "user-abc");

        Assert.Contains("user-abc/docs/", relative, StringComparison.Ordinal);
        string full = sut.GetFullPath(relative);
        Assert.True(File.Exists(full));
        Assert.Equal("hello", await File.ReadAllTextAsync(full));
    }

    [Fact]
    public void DeleteFile_RemovesExistingFile()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _basePath })
            .Build();
        var sut = new FileStorageService(config, Options.Create(new ImageProcessingOptions()));

        string path = Path.Combine(_basePath, "x", "y.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "z");

        sut.DeleteFile("x/y.txt");

        Assert.False(File.Exists(path));
    }
}