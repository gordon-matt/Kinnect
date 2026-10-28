using Heroicsoft.NetForge.Video;

namespace Kinnect.Services.Abstractions;

public interface IVideoProcessingService
{
    /// <summary>
    /// Shrinks the video at <paramref name="inputPath"/> into <paramref name="outputPath"/>
    /// when it is larger than the configured maximum. A video already inside that box, or a
    /// re-encode that is not smaller than the original, is left unchanged.
    /// </summary>
    Task<VideoTranscodeResult> CompressAsync(string inputPath, string outputPath);

    /// <summary>
    /// Extracts a JPEG frame (default: ~3s) from the video and writes it to <paramref name="outputJpegPath"/>.
    /// </summary>
    /// <returns><c>true</c> if the JPEG was written successfully.</returns>
    Task<bool> TryGenerateThumbnailAsync(string videoFilePath, string outputJpegPath, CancellationToken cancellationToken = default);
}