namespace Kinnect.Services.Abstractions;

public interface IFileStorageService
{
    void DeleteFile(string relativePath);

    string GetBaseUploadPath();

    string GetFullPath(string relativePath);

    Task<string> SaveFileAsync(Stream fileStream, string category, string fileName);

    /// <summary>
    /// Saves an image, optionally shrinking it and always generating a thumbnail.
    /// Also extracts GPS coordinates from EXIF metadata if present.
    /// Returns (ImagePath, ThumbnailPath, Latitude, Longitude).
    /// </summary>
    Task<(string ImagePath, string? ThumbnailPath, double? Latitude, double? Longitude)> SaveImageAsync(Stream fileStream, string category);
}