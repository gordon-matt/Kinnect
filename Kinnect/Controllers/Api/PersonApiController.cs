using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Kinnect.Models;
using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/people")]
[Authorize]
public class PersonApiController(IPersonService personService, IUserContextService userContextService) : ControllerBase
{
    [TranslateResultToActionResult]
    [HttpGet]
    public async Task<Result<IEnumerable<PersonDto>>> GetAll()
    {
        return await personService.GetAllAsync();
    }

    [TranslateResultToActionResult]
    [HttpGet("{id:int}")]
    public async Task<Result<PersonDto>> GetById(int id)
    {
        return await personService.GetByIdAsync(id);
    }

    [TranslateResultToActionResult]
    [HttpGet("me")]
    public async Task<Result<PersonDto>> GetCurrentPerson()
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        return await personService.GetByUserIdAsync(userId);
    }

    [TranslateResultToActionResult]
    [HttpPost]
    public async Task<Result<PersonDto>> Create([FromBody] PersonEditRequest request)
    {
        return await personService.CreateAsync(request);
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}")]
    public async Task<Result<PersonDto>> Update(int id, [FromBody] PersonEditRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        return await personService.UpdateAsync(id, request, userId);
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}/parents")]
    public async Task<Result> UpdateParents(int id, [FromBody] PersonParentLinkRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        return await personService.UpdateParentsAsync(id, request.FatherId, request.MotherId, userId);
    }

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> Delete(int id)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        return await personService.DeleteAsync(id, userId);
    }

    [TranslateResultToActionResult]
    [HttpGet("family-tree")]
    public async Task<Result<IEnumerable<FamilyTreeDatum>>> GetFamilyTreeData()
    {
        return await personService.GetFamilyTreeDataAsync();
    }

    [TranslateResultToActionResult]
    [HttpPost("{personId:int}/spouse/{spouseId:int}")]
    public async Task<Result> AddSpouse(int personId, int spouseId)
    {
        return await personService.AddSpouseAsync(personId, spouseId);
    }

    [TranslateResultToActionResult]
    [HttpDelete("{personId:int}/spouse/{spouseId:int}")]
    public async Task<Result> RemoveSpouse(int personId, int spouseId)
    {
        return await personService.RemoveSpouseAsync(personId, spouseId);
    }

    [TranslateResultToActionResult]
    [HttpGet("map-pins")]
    public async Task<Result<IEnumerable<MapPinDto>>> GetMapPins()
    {
        return await personService.GetMapPinsAsync();
    }

    [TranslateResultToActionResult]
    [HttpGet("{id:int}/versions")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result<IEnumerable<PersonVersionDto>>> GetVersions(int id)
    {
        return await personService.GetVersionsAsync(id);
    }

    [TranslateResultToActionResult]
    [HttpPost("{personId:int}/versions/{versionId:int}/restore")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> RestoreVersion(int personId, int versionId)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        return await personService.RestoreVersionAsync(personId, versionId, userId);
    }

    [TranslateResultToActionResult]
    [HttpPost("{id:int}/profile-image")]
    public async Task<Result> UploadProfileImage(int id, IFormFile file, [FromServices] IFileStorageService fileStorageService)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        using var stream = file.OpenReadStream();
        string path = await fileStorageService.SaveFileAsync(stream, Constants.FileStorage.ProfileImages, file.FileName);
        return await personService.UpdateProfileImageAsync(id, path, userId);
    }
}
