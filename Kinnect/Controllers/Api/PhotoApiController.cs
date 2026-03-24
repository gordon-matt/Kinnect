namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/photos")]
[Authorize]
public class PhotoApiController(
    IPhotoService photoService,
    IPersonService personService,
    IUserContextService userContextService,
    IFileStorageService fileStorageService) : ControllerBase
{
    private bool IsAdmin => User.IsInRole(Constants.Roles.Administrator);

    [TranslateResultToActionResult]
    [HttpGet("{id:int}")]
    public async Task<Result<PhotoDto>> GetById(int id) => await photoService.GetByIdAsync(id);

    [TranslateResultToActionResult]
    [HttpGet("person/{personId:int}")]
    public async Task<Result<IEnumerable<PhotoDto>>> GetByPerson(int personId) => await photoService.GetByPersonAsync(personId);

    [HttpPost]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string title,
        [FromForm] string? description,
        [FromForm] string? tags,
        [FromForm] int? yearTaken,
        [FromForm] int? monthTaken,
        [FromForm] int? dayTaken,
        [FromForm] int? folderId)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var personResult = await personService.GetByUserIdAsync(userId);
        if (!personResult.IsSuccess)
        {
            return Forbid();
        }

        using var stream = file.OpenReadStream();
        var (filePath, thumbnailPath, exifLat, exifLng) = await fileStorageService.SaveImageAsync(stream, Constants.FileStorage.Photos);

        var tagList = string.IsNullOrWhiteSpace(tags) ? [] : tags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

        short? y = yearTaken is >= 1 and <= 9999 ? (short)yearTaken.Value : null;
        byte? mo = monthTaken is >= 1 and <= 12 ? (byte)monthTaken.Value : null;
        byte? d = dayTaken is >= 1 and <= 31 ? (byte)dayTaken.Value : null;

        var result = await photoService.CreateAsync(title, description, filePath, thumbnailPath, personResult.Value.Id, tagList, y, mo, d, folderId, exifLat, exifLng);
        return result.IsSuccess ? Ok(result.Value) : BadRequest();
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}")]
    public async Task<Result<PhotoDto>> Update(int id, [FromBody] PhotoUpdateRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? (Result<PhotoDto>)Result.Unauthorized() : await photoService.UpdateAsync(id, request, userId, IsAdmin);
    }

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}")]
    public async Task<Result> Delete(int id)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? Result.Unauthorized() : await photoService.DeleteAsync(id, userId, IsAdmin);
    }

    #region Tag Management

    [TranslateResultToActionResult]
    [HttpPut("{id:int}/tags")]
    public async Task<Result> UpdateTags(int id, [FromBody] List<string> tags) => await photoService.UpdateTagsAsync(id, tags);

    [TranslateResultToActionResult]
    [HttpPut("{id:int}/annotations")]
    public async Task<Result> SaveAnnotations(int id, [FromBody] SaveAnnotationsRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? Result.Unauthorized() : await photoService.SaveAnnotationsAsync(id, request.AnnotationsJson, userId, IsAdmin);
    }

    [TranslateResultToActionResult]
    [HttpPost("{id:int}/tag-person/{personId:int}")]
    public async Task<Result> TagPerson(int id, int personId)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? Result.Unauthorized() : await photoService.TagPersonAsync(id, personId, userId, IsAdmin);
    }

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}/tag-person/{personId:int}")]
    public async Task<Result> UntagPerson(int id, int personId)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? Result.Unauthorized() : await photoService.UntagPersonAsync(id, personId, userId, IsAdmin);
    }

    #endregion Tag Management
}