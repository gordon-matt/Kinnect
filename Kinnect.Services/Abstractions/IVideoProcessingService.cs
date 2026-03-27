namespace Kinnect.Services.Abstractions;

public interface IVideoProcessingService
{
    /// <summary>
    /// Compresses the video at <paramref name="inputPath"/> into <paramref name="outputPath"/>
    /// using the configured quality settings.  Only resizes when the video exceeds the
    /// configured max dimensions.
    /// </summary>
    Task CompressAsync(string inputPath, string outputPath);
}