using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Extenso.Data.Entity;
using Kinnect.Data.Entities;
using Kinnect.Models;
using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/media-folders")]
[Authorize]
public class MediaFolderApiController(
    IRepository<MediaFolder> mediaFolderRepository,
    IUserContextService userContextService,
    IPersonService personService) : ControllerBase
{
    private bool IsAdmin => User.IsInRole(Constants.Roles.Administrator);

    [TranslateResultToActionResult]
    [HttpGet("person/{personId:int}")]
    public async Task<Result<IEnumerable<MediaFolderDto>>> GetByPerson(int personId)
    {
        var folders = await mediaFolderRepository.FindAsync(new SearchOptions<MediaFolder>
        {
            Query = x => x.CreatedByPersonId == personId
        });

        return Result.Success(folders
            .OrderBy(x => x.Name)
            .Select(x => new MediaFolderDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                CreatedByPersonId = x.CreatedByPersonId,
                CreatedAtUtc = x.CreatedAtUtc
            }));
    }

    [TranslateResultToActionResult]
    [HttpPost]
    public async Task<Result<MediaFolderDto>> Create([FromBody] CreateMediaFolderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Invalid(new ValidationError("Name is required."));

        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        var personResult = await personService.GetByUserIdAsync(userId);
        if (!personResult.IsSuccess)
            return Result.Forbidden();

        var folder = new MediaFolder
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedByPersonId = personResult.Value.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        await mediaFolderRepository.InsertAsync(folder);
        return Result.Success(new MediaFolderDto
        {
            Id = folder.Id,
            Name = folder.Name,
            Description = folder.Description,
            CreatedByPersonId = folder.CreatedByPersonId,
            CreatedAtUtc = folder.CreatedAtUtc
        });
    }

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}")]
    public async Task<Result> Delete(int id)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        var folder = await mediaFolderRepository.FindOneAsync(id);
        if (folder is null)
            return Result.NotFound("Folder not found.");

        if (!IsAdmin)
        {
            var personResult = await personService.GetByUserIdAsync(userId);
            if (!personResult.IsSuccess)
                return Result.Forbidden();

            if (folder.CreatedByPersonId != personResult.Value.Id)
                return Result.Forbidden();
        }

        await mediaFolderRepository.DeleteAsync(folder);
        return Result.Success();
    }
}
