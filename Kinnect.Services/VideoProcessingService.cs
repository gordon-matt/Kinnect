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
        var audioStream = mediaInfo.AudioStreams.FirstOrDefault();

        if (videoStream == null)
        {
            logger.LogWarning("No video stream found in {Path}; skipping compression.", inputPath);
            File.Copy(inputPath, outputPath, overwrite: true);
            return;
        }

        videoStream.SetCodec(VideoCodec.h264);

        // Only resize if the video is larger than the configured maximum
        if (videoStream.Width > opts.MaxWidth || videoStream.Height > opts.MaxHeight)
        {
            // Calculate new dimensions preserving aspect ratio
            double widthRatio = (double)opts.MaxWidth / videoStream.Width;
            double heightRatio = (double)opts.MaxHeight / videoStream.Height;
            double scale = Math.Min(widthRatio, heightRatio);

            // FFmpeg requires even dimensions for H.264
            int newWidth = (int)(videoStream.Width * scale) & ~1;
            int newHeight = (int)(videoStream.Height * scale) & ~1;

            videoStream.SetSize(newWidth, newHeight);
        }

        var conversion = FFmpeg.Conversions.New()
            .SetOutput(outputPath)
            .SetOverwriteOutput(true);

        conversion.AddStream(videoStream);

        if (audioStream != null)
        {
            audioStream.SetCodec(AudioCodec.aac).SetBitrate(opts.AudioBitrate);
            conversion.AddStream(audioStream);
        }

        conversion.AddParameter($"-crf {opts.Crf}");

        logger.LogInformation("Compressing video {Input} → {Output}", inputPath, outputPath);
        await conversion.Start();
    }
}