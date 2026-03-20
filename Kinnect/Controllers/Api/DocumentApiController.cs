using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Kinnect.Models;
using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentApiController(IDocumentService documentService, IPersonService personService, IUserContextService userContextService, IFileStorageService fileStorageService) : ControllerBase
{
    [TranslateResultToActionResult]
    [HttpGet("person/{personId:int}")]
    public async Task<Result<IEnumerable<DocumentDto>>> GetByPerson(int personId)
    {
        return await documentService.GetByPersonAsync(personId);
    }

    [TranslateResultToActionResult]
    [HttpGet("{id:int}")]
    public async Task<Result<DocumentDto>> GetById(int id)
    {
        return await documentService.GetByIdAsync(id);
    }

    [HttpPost]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string title, [FromForm] string? description, [FromForm] string? tags)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var personResult = await personService.GetByUserIdAsync(userId);
        if (!personResult.IsSuccess)
            return Forbid();

        using var stream = file.OpenReadStream();
        string filePath = await fileStorageService.SaveFileAsync(stream, Constants.FileStorage.Documents, file.FileName);

        var tagList = string.IsNullOrWhiteSpace(tags) ? [] : tags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

        var result = await documentService.CreateAsync(title, description, filePath, file.ContentType, file.Length, personResult.Value.Id, tagList);
        return result.IsSuccess ? Ok(result.Value) : BadRequest();
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}/tags")]
    public async Task<Result> UpdateTags(int id, [FromBody] List<string> tags)
    {
        return await documentService.UpdateTagsAsync(id, tags);
    }

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}")]
    public async Task<Result> Delete(int id)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        return await documentService.DeleteAsync(id, userId);
    }
}
