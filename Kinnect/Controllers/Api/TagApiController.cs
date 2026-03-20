using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Kinnect.Models;
using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/tags")]
[Authorize]
public class TagApiController(ITagService tagService) : ControllerBase
{
    [TranslateResultToActionResult]
    [HttpGet]
    public async Task<Result<IEnumerable<TagDto>>> GetAll()
    {
        return await tagService.GetAllAsync();
    }

    [TranslateResultToActionResult]
    [HttpGet("search")]
    public async Task<Result<IEnumerable<TagDto>>> Search([FromQuery] string q)
    {
        return await tagService.SearchAsync(q);
    }
}