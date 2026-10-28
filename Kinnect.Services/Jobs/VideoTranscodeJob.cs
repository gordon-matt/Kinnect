using Hangfire;

namespace Kinnect.Services.Jobs;

public sealed class VideoTranscodeJob(
    IRepository<Video> videoRepository,
    IFileStorageService fileStorageService,
    IVideoProcessingService videoProcessingService,
    ILogger<VideoTranscodeJob> logger)
{
    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync(int videoId, CancellationToken cancellationToken = default)
    {
        var video = await videoRepository.FindOneAsync(videoId);
        if (video is null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("VideoTranscodeJob: video {VideoId} no longer exists; skipping.", videoId);
            }

            return;
        }

        if (!video.IsProcessing)
        {
            return;
        }

        string fullInputPath = fileStorageService.GetFullPath(video.FilePath);
        string tempOutputPath = Path.ChangeExtension(fullInputPath, null) + "_compressed.mp4";

        try
        {
            var transcoded = await videoProcessingService.CompressAsync(fullInputPath, tempOutputPath);

            if (transcoded.FileReplaced && File.Exists(tempOutputPath))
            {
                string finalPath = Path.ChangeExtension(fullInputPath, ".mp4");

                if (!string.Equals(finalPath, fullInputPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(tempOutputPath, finalPath, overwrite: true);

                    if (File.Exists(fullInputPath))
                    {
                        File.Delete(fullInputPath);
                    }
                }
                else
                {
                    File.Delete(fullInputPath);
                    File.Move(tempOutputPath, finalPath);
                }

                string dir = Path.GetDirectoryName(video.FilePath)!.Replace('\\', '/');
                string nameNoExt = Path.GetFileNameWithoutExtension(video.FilePath);
                video.FilePath = $"{dir}/{nameNoExt}.mp4";
            }
            else if (File.Exists(tempOutputPath))
            {
                File.Delete(tempOutputPath);
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    transcoded.FileReplaced
                        ? "VideoTranscodeJob: replaced video {VideoId} with a smaller file."
                        : "VideoTranscodeJob: left video {VideoId} unchanged.",
                    videoId);
            }
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "VideoTranscodeJob: compression failed for video {VideoId}; original file kept.", videoId);
            }

            if (File.Exists(tempOutputPath))
            {
                File.Delete(tempOutputPath);
            }
        }
        finally
        {
            video.IsProcessing = false;
            await videoRepository.UpdateAsync(video);
        }
    }
}