using Kinnect.Services;
using Xabe.FFmpeg;

namespace Kinnect.Tests;

public class VideoProcessingServiceTests
{
    [Fact]
    public void Service_ReadsOptions()
    {
        var options = Options.Create(new VideoProcessingOptions
        {
            OutputVideoSize = VideoSize.Hd720,
            Crf = 24
        });
        var sut = new VideoProcessingService(options, NullLogger<VideoProcessingService>.Instance);

        Assert.NotNull(sut);
    }
}
