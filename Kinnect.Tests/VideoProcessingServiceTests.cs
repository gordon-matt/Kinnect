using Xabe.FFmpeg;

namespace Kinnect.Tests;

public class VideoProcessingServiceTests
{
    [Fact]
    public void Service_ReadsOptions()
    {
        var videoOptions = Options.Create(new VideoProcessingOptions
        {
            OutputVideoSize = VideoSize.Hd720,
            Crf = 24
        });
        var imageOptions = Options.Create(new ImageProcessingOptions
        {
            ThumbnailWidth = 400,
            ThumbnailHeight = 400,
            ThumbnailQuality = 70
        });
        var sut = new VideoProcessingService(videoOptions, imageOptions, NullLogger<VideoProcessingService>.Instance);

        Assert.NotNull(sut);
    }
}