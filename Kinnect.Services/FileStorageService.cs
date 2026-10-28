using Heroicsoft.NetForge.Drawing;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Directory = System.IO.Directory;

namespace Kinnect.Services;

public class FileStorageService(
    IConfiguration configuration,
    ISettingsService settings,
    IImageProcessor? images = null,
    ILogger<FileStorageService>? logger = null,
    IUploadStreamGate? streams = null) : IFileStorageService
{
    private readonly IImageProcessor processor = images ?? new ImageProcessor();

    private string BasePath => configuration["FileStorage:BasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    public async Task<bool> DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(Path.Combine(BasePath, relativePath));

        using UploadRevocation? revocation = streams?.Revoke(fullPath);
        if (revocation is { ClosedReads: > 0 })
        {
            logger?.LogInformation(
                "Closed {Count} active reads of {Path} before deleting it",
                revocation.ClosedReads,
                fullPath);
        }

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                return true;
            }
            catch (Exception ex) when (ex is (IOException or UnauthorizedAccessException) && attempt < 5)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            catch (Exception ex) when (ex is (IOException or UnauthorizedAccessException))
            {
                logger?.LogWarning(ex, "Could not delete {Path} because it is still open", fullPath);
                return false;
            }
        }
    }

    public string GetBaseUploadPath() => BasePath;

    public string GetFullPath(string relativePath) => Path.Combine(BasePath, relativePath);

    public async Task<string> SaveFileAsync(Stream fileStream, string category, string fileName, string userId)
    {
        string safeUserId = SanitizePathSegment(userId);
        string uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        string relativePath = Path.Combine(safeUserId, category, uniqueName);
        string fullPath = Path.Combine(BasePath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var fs = new FileStream(fullPath, FileMode.Create);
        await fileStream.CopyToAsync(fs);

        return relativePath.Replace('\\', '/');
    }

    public async Task<(string ImagePath, string? ThumbnailPath, double? Latitude, double? Longitude)> SaveImageAsync(Stream fileStream, string category, string userId)
    {
        var opts = await settings.GetAsync<ImageSettings>();
        string safeUserId = SanitizePathSegment(userId);

        // Buffer the stream so we can read it multiple times
        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer);
        buffer.Position = 0;

        // Extract GPS coordinates from EXIF before decoding the image
        double? latitude = null;
        double? longitude = null;
        try
        {
            buffer.Position = 0;
            var directories = ImageMetadataReader.ReadMetadata(buffer);
            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
            if (gps != null)
            {
                var location = gps.GetGeoLocation();
                if (location is { } loc)
                {
                    latitude = loc.Latitude;
                    longitude = loc.Longitude;
                }
            }
        }
        catch
        {
            // EXIF extraction is best-effort; don't fail the upload
        }

        buffer.Position = 0;

        string mainName = $"{Guid.NewGuid()}.jpg";
        string mainRelative = Path.Combine(safeUserId, category, mainName).Replace('\\', '/');
        string mainFull = Path.Combine(BasePath, mainRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(mainFull)!);

        byte[] mainBytes = await ReencodeJpegAsync(buffer, opts.MaxWidth, opts.MaxHeight, opts.Quality, opts.AutoShrinkImages);
        await File.WriteAllBytesAsync(mainFull, mainBytes);

        // Thumbnail is a second resize of the original buffer, not of the shrunk photo.
        var thumbnail = await processor.ResizeAsync(buffer, new ImageTransform
        {
            Width = opts.ThumbnailWidth,
            Height = opts.ThumbnailHeight,
            Mode = ImageScaleMode.Max,
            Format = RasterFormat.Jpeg,
            Quality = opts.ThumbnailQuality,
        });

        string thumbName = $"thumb_{Guid.NewGuid()}.jpg";
        string thumbRelative = Path.Combine(safeUserId, category, "thumbnails", thumbName).Replace('\\', '/');
        string thumbFull = Path.Combine(BasePath, thumbRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(thumbFull)!);

        await File.WriteAllBytesAsync(thumbFull, thumbnail.Bytes);

        return (mainRelative, thumbRelative, latitude, longitude);
    }

    public async Task<(string ImagePath, double? Latitude, double? Longitude)> SaveProfileImageAsync(Stream fileStream, int personId)
    {
        var opts = await settings.GetAsync<ImageSettings>();

        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer);
        buffer.Position = 0;

        double? latitude = null;
        double? longitude = null;
        try
        {
            buffer.Position = 0;
            var directories = ImageMetadataReader.ReadMetadata(buffer);
            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
            if (gps != null)
            {
                var location = gps.GetGeoLocation();
                if (location is { } loc)
                {
                    latitude = loc.Latitude;
                    longitude = loc.Longitude;
                }
            }
        }
        catch
        {
            // best-effort
        }

        buffer.Position = 0;

        string relative = Path.Combine("people", personId.ToString(), "_profile.jpg").Replace('\\', '/');
        string full = Path.Combine(BasePath, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        byte[] encoded = await ReencodeJpegAsync(buffer, opts.MaxWidth, opts.MaxHeight, opts.Quality, opts.AutoShrinkImages);
        await File.WriteAllBytesAsync(full, encoded);

        return (relative, latitude, longitude);
    }

    /// <summary>
    /// Always re-encodes to JPEG. When <paramref name="shrinkWhenLarger"/> is set and either side
    /// exceeds the box, the photo is fitted with <see cref="ImageScaleMode.Max"/>; otherwise the
    /// original pixel size is kept so a smaller image is not enlarged.
    /// </summary>
    private async Task<byte[]> ReencodeJpegAsync(Stream buffer, int maxWidth, int maxHeight, int quality, bool shrinkWhenLarger)
    {
        var info = await processor.TryIdentifyAsync(buffer)
            ?? throw new RasterDecodeException("The file is not a valid image.");

        bool shrink = shrinkWhenLarger && (info.Width > maxWidth || info.Height > maxHeight);
        var processed = await processor.ResizeAsync(buffer, new ImageTransform
        {
            Width = shrink ? maxWidth : info.Width,
            Height = shrink ? maxHeight : info.Height,
            Mode = ImageScaleMode.Max,
            Format = RasterFormat.Jpeg,
            Quality = quality,
        });

        return processed.Bytes;
    }

    /// <summary>Replaces characters that are invalid in file path segments (e.g. Keycloak subjects with '|').</summary>
    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "user";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\')
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}