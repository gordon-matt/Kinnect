using Heroicsoft.NetForge.Drawing;
using Heroicsoft.NetForge.Video;

namespace Kinnect.Services;

public class VideoProcessingService(
    ISettingsService settings,
    ILogger<VideoProcessingService> logger,
    IImageProcessor images,
    IVideoFrameService frames,
    IVideoTranscoder transcoder) : IVideoProcessingService
{
    public async Task<VideoTranscodeResult> CompressAsync(string inputPath, string outputPath)
    {
        var opts = await settings.GetAsync<VideoSettings>();

        return await transcoder.TranscodeToSizeAsync(new VideoSizeTranscodeRequest
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            VideoSizeName = string.IsNullOrWhiteSpace(opts.OutputVideoSize) ? "Hd720" : opts.OutputVideoSize,
            Crf = opts.Crf,
            AudioBitrate = opts.AudioBitrate,
        });
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
            bool captured = await frames.TryCaptureJpegAsync(
                videoFilePath,
                tempPath,
                TimeSpan.FromSeconds(3),
                cancellationToken);

            if (!captured || !File.Exists(tempPath))
            {
                return false;
            }

            string? dir = Path.GetDirectoryName(outputJpegPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Resize the raw FFmpeg snapshot to match the configured thumbnail dimensions,
            // consistent with how photo thumbnails are generated.
            var imgOpts = await settings.GetAsync<ImageSettings>(cancellationToken);
            var processed = await images.ResizeAsync(tempPath, new ImageTransform
            {
                Width = imgOpts.ThumbnailWidth,
                Height = imgOpts.ThumbnailHeight,
                Mode = ImageScaleMode.Max,
                Format = RasterFormat.Jpeg,
                Quality = imgOpts.ThumbnailQuality,
            }, cancellationToken);
            await File.WriteAllBytesAsync(outputJpegPath, processed.Bytes, cancellationToken);

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