using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Directory = System.IO.Directory;

namespace Kinnect.Services;

public class FileStorageService(IConfiguration configuration, IOptions<ImageProcessingOptions> imageOptions) : IFileStorageService
{
    private string BasePath => configuration["FileStorage:BasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    public void DeleteFile(string relativePath)
    {
        string fullPath = Path.Combine(BasePath, relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
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
        var opts = imageOptions.Value;
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

        using var mainImage = await Image.LoadAsync(buffer);

        if (opts.AutoShrinkImages && (mainImage.Width > opts.MaxWidth || mainImage.Height > opts.MaxHeight))
        {
            mainImage.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(opts.MaxWidth, opts.MaxHeight)
            }));
        }

        await mainImage.SaveAsync(mainFull, new JpegEncoder { Quality = opts.Quality });

        // Generate thumbnail
        buffer.Position = 0;
        using var thumbImage = await Image.LoadAsync(buffer);
        thumbImage.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(opts.ThumbnailWidth, opts.ThumbnailHeight)
        }));

        string thumbName = $"thumb_{Guid.NewGuid()}.jpg";
        string thumbRelative = Path.Combine(safeUserId, category, "thumbnails", thumbName).Replace('\\', '/');
        string thumbFull = Path.Combine(BasePath, thumbRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(thumbFull)!);

        await thumbImage.SaveAsync(thumbFull, new JpegEncoder { Quality = opts.ThumbnailQuality });

        return (mainRelative, thumbRelative, latitude, longitude);
    }

    public async Task<(string ImagePath, double? Latitude, double? Longitude)> SaveProfileImageAsync(Stream fileStream, int personId)
    {
        var opts = imageOptions.Value;

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

        using var image = await Image.LoadAsync(buffer);

        if (opts.AutoShrinkImages && (image.Width > opts.MaxWidth || image.Height > opts.MaxHeight))
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(opts.MaxWidth, opts.MaxHeight)
            }));
        }

        await image.SaveAsync(full, new JpegEncoder { Quality = opts.Quality });

        return (relative, latitude, longitude);
    }

    /// <summary>Replaces characters that are invalid in file path segments (e.g. Keycloak subjects with '|').</summary>
    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "user";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        var chars = value.ToCharArray();
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
