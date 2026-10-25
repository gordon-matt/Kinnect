using System.ComponentModel.DataAnnotations;

namespace Kinnect.Data.Entities;

/// <summary>How uploaded photos are resized, and how their thumbnails are made.</summary>
public class ImageSettings : AppSettings
{
    public override string? ConfigurationSection => "ImageProcessing";

    /// <summary>Resize photos larger than <see cref="MaxWidth"/> by <see cref="MaxHeight"/> when they are uploaded.</summary>
    public bool AutoShrinkImages { get; set; } = true;

    [Range(16, 20000)]
    public int MaxWidth { get; set; } = 1920;

    [Range(16, 20000)]
    public int MaxHeight { get; set; } = 1080;

    /// <summary>JPEG quality of the saved photo, 1 to 100.</summary>
    [Range(1, 100)]
    public int Quality { get; set; } = 80;

    [Range(16, 4000)]
    public int ThumbnailWidth { get; set; } = 400;

    [Range(16, 4000)]
    public int ThumbnailHeight { get; set; } = 400;

    [Range(1, 100)]
    public int ThumbnailQuality { get; set; } = 70;
}

public class ImageSettingsMap : IEntityTypeConfiguration<ImageSettings>
{
    public void Configure(EntityTypeBuilder<ImageSettings> builder)
    {
        builder.ToTable("ImageSettings", "app");
        builder.HasKey(m => m.Id);
        builder.Ignore(m => m.ConfigurationSection);
    }
}
