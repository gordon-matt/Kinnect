using Kinnect.Tests.Infrastructure;

namespace Kinnect.Tests;

public class VideoProcessingServiceTests
{
    [Fact]
    public void Service_ReadsSettings()
    {
        var settings = new TestSettingsService(
            new VideoSettings { OutputVideoSize = "Hd720", Crf = 24 },
            new ImageSettings { ThumbnailWidth = 400, ThumbnailHeight = 400, ThumbnailQuality = 70 });
        var sut = new VideoProcessingService(settings, NullLogger<VideoProcessingService>.Instance);

        Assert.NotNull(sut);
    }
}