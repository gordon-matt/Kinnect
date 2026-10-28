using System.ComponentModel.DataAnnotations;

namespace Kinnect.Data.Entities;

/// <summary>How uploaded videos are compressed with FFmpeg.</summary>
public class VideoSettings : AppSettings
{
    public const int MaxToolsPathLength = 500;

    public const int MaxOutputVideoSizeLength = 50;

    public override string? ConfigurationSection => "VideoProcessing";

    /// <summary>
    /// Folder holding <c>ffmpeg</c> and <c>ffprobe</c>. Empty looks in the usual places for the operating system,
    /// then downloads a copy next to the application.
    /// </summary>
    [StringLength(MaxToolsPathLength)]
    public string? ToolsPath { get; set; }

    /// <summary>Queue uploaded videos for compression after upload.</summary>
    public bool AutoShrinkVideos { get; set; } = true;

    /// <summary>
    /// Maximum picture size, such as <c>Hd720</c> (1280×720). A video already inside that box is kept
    /// as uploaded, except a sideways one, which is turned upright when that does not make the file much larger.
    /// A larger one is shrunk to fit, and the result is discarded if it is not a smaller file.
    /// Kept as text so this project does not need FFmpeg.
    /// </summary>
    [Required, StringLength(MaxOutputVideoSizeLength)]
    public string OutputVideoSize { get; set; } = "Hd720";

    /// <summary>H.264 Constant Rate Factor, 0 to 51. Lower is better quality and a larger file. 18 to 28 is a sensible range.</summary>
    [Range(0, 51)]
    public int Crf { get; set; } = 28;

    /// <summary>Audio bitrate in bits per second. 128000 is 128 kbps.</summary>
    [Range(8_000, 512_000)]
    public int AudioBitrate { get; set; } = 128_000;
}

public class VideoSettingsMap : IEntityTypeConfiguration<VideoSettings>
{
    public void Configure(EntityTypeBuilder<VideoSettings> builder)
    {
        builder.ToTable("VideoSettings", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.ToolsPath).HasMaxLength(VideoSettings.MaxToolsPathLength);
        builder.Property(m => m.OutputVideoSize).IsRequired().HasMaxLength(VideoSettings.MaxOutputVideoSizeLength).IsUnicode(false);
        builder.Ignore(m => m.ConfigurationSection);
    }
}
