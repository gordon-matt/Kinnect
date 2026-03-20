using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Kinnect.Models;
using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/feed")]
[Authorize]
public class FeedApiController(IFeedService feedService) : ControllerBase
{
    [TranslateResultToActionResult]
    [HttpGet]
    public async Task<Result<IEnumerable<FeedItemDto>>> GetFeed([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return await feedService.GetFeedAsync(page, pageSize);
    }
}
