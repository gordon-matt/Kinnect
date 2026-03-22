using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Kinnect.Models;
using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/posts")]
[Authorize]
public class PostApiController(IPostService postService, IPersonService personService, IUserContextService userContextService) : ControllerBase
{
    [TranslateResultToActionResult]
    [HttpGet]
    public async Task<Result<IEnumerable<PostDto>>> GetRecent([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return await postService.GetRecentAsync(page, pageSize);
    }

    [TranslateResultToActionResult]
    [HttpGet("person/{personId:int}")]
    public async Task<Result<IEnumerable<PostDto>>> GetByPerson(int personId)
    {
        return await postService.GetByPersonAsync(personId);
    }

    [TranslateResultToActionResult]
    [HttpGet("person/{personId:int}/paged")]
    public async Task<Result<PagedItems<PostDto>>> GetByPersonPaged(int personId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        return await postService.GetByPersonPagedAsync(personId, page, pageSize);
    }

    [TranslateResultToActionResult]
    [HttpPost]
    public async Task<Result<PostDto>> Create([FromBody] PostCreateRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        var personResult = await personService.GetByUserIdAsync(userId);
        if (!personResult.IsSuccess)
            return Result.Forbidden();

        return await postService.CreateAsync(request, personResult.Value.Id);
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}")]
    public async Task<Result<PostDto>> Update(int id, [FromBody] PostEditRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        return await postService.UpdateAsync(id, request, userId);
    }

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}")]
    public async Task<Result> Delete(int id)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        return await postService.DeleteAsync(id, userId);
    }
}