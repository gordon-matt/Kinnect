namespace Kinnect.Services.Abstractions;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string category, string fileName);

    /// <summary>
    /// Saves an image, optionally shrinking it and always generating a thumbnail.
    /// Returns (imagePath, thumbnailPath).
    /// </summary>
    Task<(string ImagePath, string? ThumbnailPath)> SaveImageAsync(Stream fileStream, string category, string fileName);

    void DeleteFile(string relativePath);

    string GetFullPath(string relativePath);

    string GetBaseUploadPath();
}