namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/feed")]
[Authorize]
public class FeedApiController(IFeedService feedService) : ControllerBase
{
    [TranslateResultToActionResult]
    [HttpGet]
    public async Task<Result<IEnumerable<FeedItemDto>>> GetFeed([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await feedService.GetFeedAsync(page, pageSize);
}