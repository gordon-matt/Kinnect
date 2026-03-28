using Kinnect.Models.FamilyTree;

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
    public async Task<Result<IEnumerable<PersonDto>>> GetAll() => await personService.GetAllAsync();

    [TranslateResultToActionResult]
    [HttpGet("{id:int}")]
    public async Task<Result<PersonDto>> GetById(int id) => await personService.GetByIdAsync(id);

    [TranslateResultToActionResult]
    [HttpGet("me")]
    public async Task<Result<PersonDto>> GetCurrentPerson()
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? (Result<PersonDto>)Result.Unauthorized() : await personService.GetByUserIdAsync(userId);
    }

    [HttpGet("users")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await userInfoService.GetAllUsersAsync();
        return Ok(users);
    }

    [TranslateResultToActionResult]
    [HttpPost]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result<PersonDto>> Create([FromBody] PersonEditRequest request) => await personService.CreateAsync(request);

    [TranslateResultToActionResult]
    [HttpPut("{id:int}")]
    public async Task<Result<PersonDto>> Update(int id, [FromBody] PersonEditRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? (Result<PersonDto>)Result.Unauthorized() : await personService.UpdateAsync(id, request, userId, IsAdmin);
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}/parents")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> UpdateParents(int id, [FromBody] PersonParentLinkRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null
            ? Result.Unauthorized()
            : await personService.UpdateParentsAsync(id, request.FatherId, request.MotherId, userId, isAdmin: true);
    }

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> Delete(int id)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? Result.Unauthorized() : await personService.DeleteAsync(id, userId);
    }

    [TranslateResultToActionResult]
    [HttpGet("family-tree")]
    public async Task<Result<IEnumerable<FamilyTreeDatum>>> GetFamilyTreeData() => await personService.GetFamilyTreeDataAsync();

    [TranslateResultToActionResult]
    [HttpGet("map-pins")]
    public async Task<Result<IEnumerable<MapPinDto>>> GetMapPins() => await personService.GetMapPinsAsync();

    [TranslateResultToActionResult]
    [HttpPost("{id:int}/profile-image")]
    public async Task<Result<object>> UploadProfileImage(int id, IFormFile file, [FromServices] IFileStorageService fileStorageService)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
        {
            return Result.Unauthorized();
        }

        using var stream = file.OpenReadStream();
        var (imagePath, _, _) = await fileStorageService.SaveProfileImageAsync(stream, userId);
        var updateResult = await personService.UpdateProfileImageAsync(id, imagePath, userId, IsAdmin);
        return !updateResult.IsSuccess ? (Result<object>)Result.Forbidden() : Result.Success<object>(new { imagePath });
    }

    #region Spouse

    [TranslateResultToActionResult]
    [HttpPost("{personId:int}/spouse/{spouseId:int}")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> AddSpouse(int personId, int spouseId) => await personService.AddSpouseAsync(personId, spouseId);

    [TranslateResultToActionResult]
    [HttpDelete("{personId:int}/spouse/{spouseId:int}")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> RemoveSpouse(int personId, int spouseId) => await personService.RemoveSpouseAsync(personId, spouseId);

    [TranslateResultToActionResult]
    [HttpGet("{id:int}/spouses")]
    public async Task<Result<IEnumerable<PersonSpouseDetailDto>>> GetSpouses(int id) => await personService.GetSpousesForPersonAsync(id);

    [TranslateResultToActionResult]
    [HttpPut("{personId:int}/spouse/{spouseId:int}")]
    public async Task<Result> UpdateSpouseRelationship(int personId, int spouseId, [FromBody] PersonSpouseUpdateRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null
            ? Result.Unauthorized()
            : await personService.UpdateSpouseRelationshipAsync(personId, spouseId, request, userId, IsAdmin);
    }

    #endregion Spouse

    #region Link/Unlink User Account

    [TranslateResultToActionResult]
    [HttpPost("{id:int}/link-user")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> LinkUserAccount(int id, [FromBody] LinkUserAccountRequest request) => await personService.LinkUserAccountAsync(id, request.UserId);

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}/link-user")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> UnlinkUserAccount(int id)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null
            ? Result.Unauthorized()
            : await personService.UnlinkUserAccountAsync(id, userId);
    }

    #endregion Link/Unlink User Account

    #region Versioning

    [TranslateResultToActionResult]
    [HttpGet("{id:int}/versions")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result<IEnumerable<PersonVersionDto>>> GetVersions(int id) => await personService.GetVersionsAsync(id);

    [TranslateResultToActionResult]
    [HttpPost("{personId:int}/versions/{versionId:int}/restore")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<Result> RestoreVersion(int personId, int versionId)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? Result.Unauthorized() : await personService.RestoreVersionAsync(personId, versionId, userId);
    }

    #endregion Versioning
}