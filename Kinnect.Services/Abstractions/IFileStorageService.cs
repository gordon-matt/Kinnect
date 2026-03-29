namespace Kinnect.Services.Abstractions;

public interface IFileStorageService
{
    void DeleteFile(string relativePath);

    string GetBaseUploadPath();

    string GetFullPath(string relativePath);

    /// <summary>Saves a file under <c>{userId}/{category}/{uniqueName}</c>.</summary>
    Task<string> SaveFileAsync(Stream fileStream, string category, string fileName, string userId);

    /// <summary>
    /// Saves an image under <c>{userId}/{category}/</c>, optionally shrinking it and generating a thumbnail under
    /// <c>{userId}/{category}/thumbnails/</c>. Also extracts GPS coordinates from EXIF metadata if present.
    /// </summary>
    Task<(string ImagePath, string? ThumbnailPath, double? Latitude, double? Longitude)> SaveImageAsync(Stream fileStream, string category, string userId);

    /// <summary>
    /// Saves a single profile image as <c>people/{personId}/_profile.jpg</c> (processed JPEG, same resize rules as other images).
    /// </summary>
    Task<(string ImagePath, double? Latitude, double? Longitude)> SaveProfileImageAsync(Stream fileStream, int personId);
}
