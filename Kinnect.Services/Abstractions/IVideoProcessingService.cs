namespace Kinnect.Services.Abstractions;

public interface IVideoProcessingService
{
    /// <summary>
    /// Compresses the video at <paramref name="inputPath"/> into <paramref name="outputPath"/>
    /// using the configured quality settings.  Only resizes when the video exceeds the
    /// configured max dimensions.
    /// </summary>
    Task CompressAsync(string inputPath, string outputPath);

    /// <summary>
    /// Extracts a JPEG frame (default: ~3s) from the video and writes it to <paramref name="outputJpegPath"/>.
    /// </summary>
    /// <returns><c>true</c> if the JPEG was written successfully.</returns>
    Task<bool> TryGenerateThumbnailAsync(string videoFilePath, string outputJpegPath, CancellationToken cancellationToken = default);
}