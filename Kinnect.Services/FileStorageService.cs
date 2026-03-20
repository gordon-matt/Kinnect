using Kinnect.Services.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Kinnect.Services;

public class FileStorageService(IConfiguration configuration) : IFileStorageService
{
    private string BasePath => configuration["FileStorage:BasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    public async Task<string> SaveFileAsync(Stream fileStream, string category, string fileName)
    {
        string uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        string relativePath = Path.Combine(category, DateTime.UtcNow.ToString("yyyy/MM"), uniqueName);
        string fullPath = Path.Combine(BasePath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var fs = new FileStream(fullPath, FileMode.Create);
        await fileStream.CopyToAsync(fs);

        return relativePath.Replace('\\', '/');
    }

    public void DeleteFile(string relativePath)
    {
        string fullPath = Path.Combine(BasePath, relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public string GetFullPath(string relativePath) => Path.Combine(BasePath, relativePath);

    public string GetBaseUploadPath() => BasePath;
}
