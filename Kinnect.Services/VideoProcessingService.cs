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
}
