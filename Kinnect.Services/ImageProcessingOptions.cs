namespace Kinnect.Services;

public class ImageProcessingOptions
{
    public bool AutoShrinkImages { get; set; } = true;

    public int MaxWidth { get; set; } = 1920;

    public int MaxHeight { get; set; } = 1080;

    public int Quality { get; set; } = 80;

    public int ThumbnailWidth { get; set; } = 400;

    public int ThumbnailHeight { get; set; } = 400;

    public int ThumbnailQuality { get; set; } = 70;
}