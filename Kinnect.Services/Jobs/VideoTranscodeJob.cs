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
            await videoProcessingService.CompressAsync(fullInputPath, tempOutputPath);

            if (File.Exists(fullInputPath))
            {
                File.Delete(fullInputPath);
            }

            File.Move(tempOutputPath, fullInputPath);

            string dir = Path.GetDirectoryName(video.FilePath)!.Replace('\\', '/');
            string nameNoExt = Path.GetFileNameWithoutExtension(video.FilePath);
            video.FilePath = $"{dir}/{nameNoExt}.mp4";

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("VideoTranscodeJob: finished compression for video {VideoId}.", videoId);
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