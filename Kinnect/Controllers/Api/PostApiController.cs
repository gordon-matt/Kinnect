namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/posts")]
[Authorize]
public class PostApiController(IPostService postService, IPersonService personService, IUserContextService userContextService) : ControllerBase
{
    [TranslateResultToActionResult]
    [HttpGet]
    public async Task<Result<IEnumerable<PostDto>>> GetRecent([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await postService.GetRecentAsync(page, pageSize);

    [TranslateResultToActionResult]
    [HttpGet("person/{personId:int}")]
    public async Task<Result<IEnumerable<PostDto>>> GetByPerson(int personId) => await postService.GetByPersonAsync(personId);

    [TranslateResultToActionResult]
    [HttpGet("person/{personId:int}/paged")]
    public async Task<Result<PagedApiResponse<PostDto>>> GetByPersonPaged(int personId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await postService.GetByPersonPagedAsync(personId, page, pageSize);
        if (!result.IsSuccess)
        {
            return Result<PagedApiResponse<PostDto>>.Error(string.Join("; ", result.Errors));
        }

        var p = result.Value;
        return Result.Success(new PagedApiResponse<PostDto>
        {
            Items = p.ToList(),
            TotalCount = p.ItemCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [TranslateResultToActionResult]
    [HttpPost]
    public async Task<Result<PostDto>> Create([FromBody] PostCreateRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
        {
            return Result.Unauthorized();
        }

        var personResult = await personService.GetByUserIdAsync(userId);
        return !personResult.IsSuccess ? (Result<PostDto>)Result.Forbidden() : await postService.CreateAsync(request, personResult.Value.Id);
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}")]
    public async Task<Result<PostDto>> Update(int id, [FromBody] PostEditRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? (Result<PostDto>)Result.Unauthorized() : await postService.UpdateAsync(id, request, userId);
    }

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}")]
    public async Task<Result> Delete(int id)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? Result.Unauthorized() : await postService.DeleteAsync(id, userId);
    }
}