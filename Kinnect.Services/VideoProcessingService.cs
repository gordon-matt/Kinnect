using Microsoft.Extensions.Options;
using Xabe.FFmpeg;

namespace Kinnect.Services;

public class VideoProcessingService(
    IOptions<VideoProcessingOptions> videoOptions,
    ILogger<VideoProcessingService> logger) : IVideoProcessingService
{
    public async Task CompressAsync(string inputPath, string outputPath)
    {
        var opts = videoOptions.Value;

        var mediaInfo = await FFmpeg.GetMediaInfo(inputPath);

        var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
        if (videoStream == null)
        {
            logger.LogWarning("No video stream found in {Path}; skipping compression.", inputPath);
            File.Copy(inputPath, outputPath, overwrite: true);
            return;
        }

        logger.LogInformation(
            "Compressing video {Input} → {Output} (ChangeSize {VideoSize}, -noautorotate)",
            inputPath,
            outputPath,
            opts.OutputVideoSize);

        IConversion conversion = await FFmpeg.Conversions.FromSnippet.ChangeSize(inputPath, outputPath, opts.OutputVideoSize);
        conversion.SetOverwriteOutput(true);
        conversion.AddParameter("-noautorotate", ParameterPosition.PreInput);
        conversion.AddParameter("-c:v libx264");
        conversion.AddParameter($"-crf {opts.Crf}");
        conversion.AddParameter("-c:a aac");
        conversion.AddParameter($"-b:a {opts.AudioBitrate}");

        await conversion.Start();
    }

    public async Task<bool> TryGenerateThumbnailAsync(string videoFilePath, string outputJpegPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(videoFilePath))
        {
            return false;
        }

        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");

        try
        {
            var conversion = await FFmpeg.Conversions.FromSnippet.Snapshot(
                videoFilePath, tempPath, TimeSpan.FromSeconds(3));

            conversion.SetOverwriteOutput(true);

            cancellationToken.ThrowIfCancellationRequested();
            await conversion.Start();
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(tempPath))
            {
                return false;
            }

            byte[] bytes = await File.ReadAllBytesAsync(tempPath, cancellationToken);
            string? dir = Path.GetDirectoryName(outputJpegPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllBytesAsync(outputJpegPath, bytes, cancellationToken);
            return File.Exists(outputJpegPath);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "Failed to generate thumbnail from video {Path}", videoFilePath);
            }

            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // ignore temp cleanup failures
            }
        }
    }
}
