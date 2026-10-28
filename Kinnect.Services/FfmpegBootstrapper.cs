using Heroicsoft.NetForge.Video;
using Microsoft.Extensions.Hosting;

namespace Kinnect.Services;

/// <summary>
/// Configures the shared FFmpeg directory at startup: the path from video settings, common OS
/// locations, or a downloaded official build under the content root.
/// </summary>
public static class FfmpegBootstrapper
{
    public static Task EnsureConfiguredAsync(
        string? configuredToolsPath,
        IHostEnvironment hostEnvironment,
        ILogger logger,
        CancellationToken cancellationToken = default) =>
        FfmpegBootstrap.EnsureAsync(
            new FfmpegBootstrapOptions
            {
                ConfiguredPath = configuredToolsPath,
                DownloadDirectory = Path.Combine(hostEnvironment.ContentRootPath, "ffmpeg-tools"),
                SearchWellKnownLocations = true,
                DownloadIfMissing = true,
                RequireFfprobe = true,
            },
            logger,
            cancellationToken);
}
