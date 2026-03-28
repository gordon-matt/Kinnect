using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Kinnect.Services;

/// <summary>
/// Configures Xabe.FFmpeg static paths at startup: optional directory from config, common OS locations,
/// or a downloaded official build (see <see cref="VideoProcessingOptions.ToolsPath"/>).
/// </summary>
public static class FfmpegBootstrapper
{
    public static async Task EnsureConfiguredAsync(
        IOptions<VideoProcessingOptions> videoOptions,
        IHostEnvironment hostEnvironment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? configured = videoOptions.Value.ToolsPath?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            string dir = Path.GetFullPath(configured);
            if (HasFfmpegPair(dir))
            {
                FFmpeg.SetExecutablesPath(dir);
                logger.LogInformation("FFmpeg tools directory (from VideoProcessing:ToolsPath): {Path}", dir);
                return;
            }

            logger.LogWarning(
                "VideoProcessing:ToolsPath is set to '{Path}' but ffmpeg/ffprobe were not found there; trying other locations.",
                dir);
        }

        foreach (string candidate in GetWellKnownDirectories())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HasFfmpegPair(candidate))
            {
                continue;
            }

            FFmpeg.SetExecutablesPath(candidate);
            logger.LogInformation("FFmpeg tools directory (auto-detected): {Path}", candidate);
            return;
        }

        string downloadDir = Path.Combine(hostEnvironment.ContentRootPath, "ffmpeg-tools");
        Directory.CreateDirectory(downloadDir);

        if (HasFfmpegPair(downloadDir))
        {
            FFmpeg.SetExecutablesPath(downloadDir);
            logger.LogInformation("FFmpeg tools directory (existing cache): {Path}", downloadDir);
            return;
        }

        try
        {
            logger.LogInformation("Downloading FFmpeg (official build) to {Path} …", downloadDir);
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, downloadDir, null);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to download FFmpeg. Install ffmpeg on the host, set PATH, or set VideoProcessing:ToolsPath to the folder containing ffmpeg and ffprobe.");
            return;
        }

        if (!HasFfmpegPair(downloadDir))
        {
            logger.LogError(
                "FFmpeg download finished but ffmpeg/ffprobe were not found under {Path}. Check write permissions and disk space.",
                downloadDir);
            return;
        }

        FFmpeg.SetExecutablesPath(downloadDir);
        logger.LogInformation("FFmpeg tools directory (downloaded): {Path}", downloadDir);
    }

    private static bool HasFfmpegPair(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        bool windows = OperatingSystem.IsWindows();
        string ffmpeg = Path.Combine(directory, windows ? "ffmpeg.exe" : "ffmpeg");
        string ffprobe = Path.Combine(directory, windows ? "ffprobe.exe" : "ffprobe");
        return File.Exists(ffmpeg) && File.Exists(ffprobe);
    }

    private static IEnumerable<string> GetWellKnownDirectories()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg");
        }

        yield return "/usr/bin";
        yield return "/usr/local/bin";
    }
}