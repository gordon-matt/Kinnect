using Hangfire;
using Kinnect.Services.Jobs;
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
    IBackgroundJobClient backgroundJobClient,
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
    [RequestSizeLimit(Constants.MaxUploadBytes)]
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
        string filePath = await fileStorageService.SaveFileAsync(stream, Constants.FileStorage.Videos, file.FileName, userId);

        string? thumbnailPath = null;
        string fullVideoPath = fileStorageService.GetFullPath(filePath);
        string? videoDir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(videoDir))
        {
            string thumbFile = Path.ChangeExtension(Path.GetFileName(filePath), ".jpg");
            thumbnailPath = Path.Combine(videoDir, "thumbnails", thumbFile).Replace('\\', '/');
            string thumbFullPath = fileStorageService.GetFullPath(thumbnailPath);
            bool thumbOk = await videoProcessingService.TryGenerateThumbnailAsync(
                fullVideoPath,
                thumbFullPath,
                HttpContext.RequestAborted);
            if (!thumbOk)
            {
                thumbnailPath = null;
            }
        }

        var opts = videoOptions.Value;
        bool queueTranscode = opts.AutoShrinkVideos;

        var tagList = string.IsNullOrWhiteSpace(tags) ? [] : tags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

        var result = await videoService.CreateAsync(
            title,
            description,
            filePath,
            thumbnailPath,
            null,
            personResult.Value.Id,
            tagList,
            folderId,
            isProcessing: queueTranscode);

        if (!result.IsSuccess)
        {
            return BadRequest();
        }

        if (queueTranscode)
        {
            backgroundJobClient.Enqueue<VideoTranscodeJob>(
                job => job.ExecuteAsync(result.Value.Id, CancellationToken.None));
        }

        return Ok(result.Value);
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