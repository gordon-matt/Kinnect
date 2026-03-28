namespace Kinnect.Services;

public class VideoProcessingOptions
{
    /// <summary>
    /// Directory containing <c>ffmpeg</c> and <c>ffprobe</c> (same folder). Empty = probe <c>/usr/bin</c>, Windows defaults,
    /// then download under the app content root (<c>ffmpeg-tools</c>) via <see cref="FfmpegBootstrapper"/>.
    /// </summary>
    public string? ToolsPath { get; set; }

    /// <summary>When true, videos exceeding MaxWidth/MaxHeight are compressed and resized.</summary>
    public bool AutoShrinkVideos { get; set; } = true;

    /// <summary>Maximum output width in pixels.</summary>
    public int MaxWidth { get; set; } = 1280;

    /// <summary>Maximum output height in pixels.</summary>
    public int MaxHeight { get; set; } = 720;

    /// <summary>
    /// H.264 Constant Rate Factor (0–51). Lower = better quality / larger file.
    /// Recommended range: 18–28. Default 28 balances quality and size.
    /// </summary>
    public int Crf { get; set; } = 28;

    /// <summary>Audio bitrate in bits per second (e.g. 128000 = 128 kbps).</summary>
    public int AudioBitrate { get; set; } = 128_000;
}