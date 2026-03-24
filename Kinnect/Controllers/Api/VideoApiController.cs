using Kinnect.Services;
using Microsoft.Extensions.Options;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/videos")]
[Authorize]
public class VideoApiController(
    IVideoService videoService,
    IPersonService personService,
    IUserContextService userContextService,
    IFileStorageService fileStorageService,
    IVideoProcessingService videoProcessingService,
    IOptions<VideoProcessingOptions> videoOptions) : ControllerBase
{
    private bool IsAdmin => User.IsInRole(Constants.Roles.Administrator);

    [TranslateResultToActionResult]
    [HttpGet("person/{personId:int}")]
    public async Task<Result<IEnumerable<VideoDto>>> GetByPerson(int personId) => await videoService.GetByPersonAsync(personId);

    [TranslateResultToActionResult]
    [HttpGet("{id:int}")]
    public async Task<Result<VideoDto>> GetById(int id) => await videoService.GetByIdAsync(id);

    [HttpPost]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string title, [FromForm] string? description, [FromForm] string? tags, [FromForm] int? folderId)
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
        string filePath = await fileStorageService.SaveFileAsync(stream, Constants.FileStorage.Videos, file.FileName);

        var opts = videoOptions.Value;
        if (opts.AutoShrinkVideos)
        {
            string fullInputPath = fileStorageService.GetFullPath(filePath);
            string tempOutputPath = Path.ChangeExtension(fullInputPath, null) + "_compressed.mp4";

            try
            {
                await videoProcessingService.CompressAsync(fullInputPath, tempOutputPath);

                // Replace the original with the compressed version
                System.IO.File.Delete(fullInputPath);
                System.IO.File.Move(tempOutputPath, fullInputPath);

                // Normalise the stored path to use .mp4 extension
                string dir = Path.GetDirectoryName(filePath)!.Replace('\\', '/');
                string nameNoExt = Path.GetFileNameWithoutExtension(filePath);
                filePath = $"{dir}/{nameNoExt}.mp4";
            }
            catch (Exception ex)
            {
                // Compression is best-effort; keep the original if it fails
                if (System.IO.File.Exists(tempOutputPath))
                {
                    System.IO.File.Delete(tempOutputPath);
                }

                // Re-throw so the caller knows something went wrong
                return StatusCode(500, $"Video compression failed: {ex.Message}");
            }
        }

        var tagList = string.IsNullOrWhiteSpace(tags) ? [] : tags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

        var result = await videoService.CreateAsync(title, description, filePath, null, null, personResult.Value.Id, tagList, folderId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest();
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}")]
    public async Task<Result<VideoDto>> Update(int id, [FromBody] VideoUpdateRequest request)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? (Result<VideoDto>)Result.Unauthorized() : await videoService.UpdateAsync(id, request, userId, IsAdmin);
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}/tags")]
    public async Task<Result> UpdateTags(int id, [FromBody] List<string> tags) => await videoService.UpdateTagsAsync(id, tags);

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}")]
    public async Task<Result> Delete(int id)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? Result.Unauthorized() : await videoService.DeleteAsync(id, userId, IsAdmin);
    }
}
