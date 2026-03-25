namespace Kinnect.Tests;

public class VideoProcessingServiceTests
{
    [Fact]
    public void Service_ReadsOptions()
    {
        var options = Options.Create(new VideoProcessingOptions
        {
            MaxWidth = 640,
            MaxHeight = 480,
            Crf = 24
        });
        var sut = new VideoProcessingService(options, NullLogger<VideoProcessingService>.Instance);

        Assert.NotNull(sut);
    }
}