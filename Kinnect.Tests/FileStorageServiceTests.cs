using Kinnect.Tests.Infrastructure;

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
        var sut = new FileStorageService(config, new TestSettingsService());

        await using var stream = new MemoryStream("hello"u8.ToArray());
        string relative = await sut.SaveFileAsync(stream, "docs", "note.txt", "user-abc");

        Assert.Contains("user-abc/docs/", relative, StringComparison.Ordinal);
        string full = sut.GetFullPath(relative);
        Assert.True(File.Exists(full));
        Assert.Equal("hello", await File.ReadAllTextAsync(full));
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesExistingFile()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _basePath })
            .Build();
        var sut = new FileStorageService(config, new TestSettingsService());

        string path = Path.Combine(_basePath, "x", "y.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "z");

        Assert.True(await sut.DeleteFileAsync("x/y.txt"));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesAFileThatAllowsDeleteSharing()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _basePath })
            .Build();
        var sut = new FileStorageService(config, new TestSettingsService());

        string path = Path.Combine(_basePath, "x", "open.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "video");

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        Assert.True(await sut.DeleteFileAsync("x/open.mp4"));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DeleteFileAsync_ClosesEveryActiveReaderThenDeletes()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _basePath })
            .Build();
        var gate = new UploadStreamGate();
        var sut = new FileStorageService(config, new TestSettingsService(), streams: gate);

        string path = Path.Combine(_basePath, "x", "shared.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, new byte[32]);

        Stream first = gate.OpenRead(path);
        Stream second = gate.OpenRead(path);

        Assert.True(await sut.DeleteFileAsync("x/shared.mp4"));
        Assert.False(File.Exists(path));
        Assert.True(first is IRevocableRead { Revoked: true });
        Assert.True(second is IRevocableRead { Revoked: true });
        Assert.Equal(0, first.Read(new byte[1], 0, 1));
        Assert.Equal(0, second.Read(new byte[1], 0, 1));

        await first.DisposeAsync();
        await second.DisposeAsync();
    }
}