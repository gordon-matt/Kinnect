namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/tags")]
[Authorize]
public class TagApiController(ITagService tagService) : ControllerBase
{
    [TranslateResultToActionResult]
    [HttpGet]
    public async Task<Result<IEnumerable<TagDto>>> GetAll() => await tagService.GetAllAsync();

    [TranslateResultToActionResult]
    [HttpGet("search")]
    public async Task<Result<IEnumerable<TagDto>>> Search([FromQuery] string q) => await tagService.SearchAsync(q);
}