using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Kinnect.Models;
using Kinnect.Services;
using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/people")]
[Authorize]
public class PersonApiController(
    IPersonService personService,
    IUserContextService userContextService,
    IUserInfoService userInfoService) : ControllerBase
{
    private bool IsAdmin => User.IsInRole(Constants.Roles.Administrator);

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
    [Authorize(Roles = Constants.Roles.Administrator)]
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

        return await personService.UpdateAsync(id, request, userId, IsAdmin);
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}/parents")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> UpdateParents(int id, [FromBody] PersonParentLinkRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        return await personService.UpdateParentsAsync(id, request.FatherId, request.MotherId, userId, isAdmin: true);
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
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> AddSpouse(int personId, int spouseId)
    {
        return await personService.AddSpouseAsync(personId, spouseId);
    }

    [TranslateResultToActionResult]
    [HttpDelete("{personId:int}/spouse/{spouseId:int}")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> RemoveSpouse(int personId, int spouseId)
    {
        return await personService.RemoveSpouseAsync(personId, spouseId);
    }

    [TranslateResultToActionResult]
    [HttpGet("{id:int}/spouses")]
    public async Task<Result<IEnumerable<PersonSpouseDetailDto>>> GetSpouses(int id)
    {
        return await personService.GetSpousesForPersonAsync(id);
    }

    [TranslateResultToActionResult]
    [HttpPut("{personId:int}/spouse/{spouseId:int}")]
    public async Task<Result> UpdateSpouseRelationship(int personId, int spouseId, [FromBody] PersonSpouseUpdateRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        return await personService.UpdateSpouseRelationshipAsync(personId, spouseId, request, userId, IsAdmin);
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
    public async Task<Result<object>> UploadProfileImage(int id, IFormFile file, [FromServices] IFileStorageService fileStorageService)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
            return Result.Unauthorized();

        using var stream = file.OpenReadStream();
        var (imagePath, _) = await fileStorageService.SaveImageAsync(stream, Constants.FileStorage.ProfileImages, file.FileName);
        var updateResult = await personService.UpdateProfileImageAsync(id, imagePath, userId, IsAdmin);
        if (!updateResult.IsSuccess)
            return Result.Forbidden();

        return Result.Success<object>(new { imagePath });
    }

    [TranslateResultToActionResult]
    [HttpPost("{id:int}/link-user")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> LinkUserAccount(int id, [FromBody] LinkUserAccountRequest request)
    {
        return await personService.LinkUserAccountAsync(id, request.UserId);
    }

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}/link-user")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> UnlinkUserAccount(int id)
    {
        return await personService.UnlinkUserAccountAsync(id);
    }

    [HttpGet("users")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await userInfoService.GetAllUsersAsync();
        return Ok(users);
    }
}
