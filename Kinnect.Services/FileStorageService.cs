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

    public async Task<(string ImagePath, string? ThumbnailPath, double? Latitude, double? Longitude)> SaveImageAsync(Stream fileStream, string category)
    {
        var opts = imageOptions.Value;

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

        string datePart = DateTime.UtcNow.ToString("yyyy/MM");
        string mainName = $"{Guid.NewGuid()}.jpg";
        string mainRelative = Path.Combine(category, datePart, mainName).Replace('\\', '/');
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
        string thumbRelative = Path.Combine(Constants.FileStorage.Thumbnails, datePart, thumbName).Replace('\\', '/');
        string thumbFull = Path.Combine(BasePath, thumbRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(thumbFull)!);

        await thumbImage.SaveAsync(thumbFull, new JpegEncoder { Quality = opts.ThumbnailQuality });

        return (mainRelative, thumbRelative, latitude, longitude);
    }
}