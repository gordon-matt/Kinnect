using Microsoft.Extensions.Options;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentApiController(
    IDocumentService documentService,
    IPersonService personService,
    IUserContextService userContextService,
    IFileStorageService fileStorageService,
    IOptions<DocumentProcessingOptions> docOptions) : ControllerBase
{
    private static readonly HashSet<string> ImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    [TranslateResultToActionResult]
    [HttpGet("{id:int}")]
    public async Task<Result<DocumentDto>> GetById(int id) => await documentService.GetByIdAsync(id);

    [TranslateResultToActionResult]
    [HttpGet("person/{personId:int}")]
    public async Task<Result<IEnumerable<DocumentDto>>> GetByPerson(int personId) => await documentService.GetByPersonAsync(personId);

    [TranslateResultToActionResult]
    [HttpDelete("{id:int}")]
    public async Task<Result> Delete(int id)
    {
        string? userId = userContextService.GetCurrentUserId();
        return userId is null ? Result.Unauthorized() : await documentService.DeleteAsync(id, userId);
    }

    [TranslateResultToActionResult]
    [HttpPut("{id:int}/tags")]
    public async Task<Result> UpdateTags(int id, [FromBody] List<string> tags) => await documentService.UpdateTagsAsync(id, tags);

    [HttpPost]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string title, [FromForm] string? description, [FromForm] string? tags)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var opts = docOptions.Value;
        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!opts.AllowedExtensionSet.Contains(ext))
        {
            return BadRequest($"File type '{ext}' is not allowed. Allowed types: {opts.AllowedExtensions}");
        }

        if (file.Length > opts.MaxFileSizeBytes)
        {
            return BadRequest($"File size exceeds the maximum allowed size of {opts.MaxFileSizeBytes / 1_048_576.0:F1} MB.");
        }

        var personResult = await personService.GetByUserIdAsync(userId);
        if (!personResult.IsSuccess)
        {
            return Forbid();
        }

        string filePath;
        string contentType = file.ContentType;
        long fileSize = file.Length;

        if (opts.AutoShrinkDocuments && ImageExtensions.Contains(ext))
        {
            // Process images through the same resize pipeline as photos
            using var stream = file.OpenReadStream();
            var (imagePath, _, _, _) = await fileStorageService.SaveImageAsync(stream, Constants.FileStorage.Documents);
            filePath = imagePath;
            contentType = "image/jpeg";

            // Re-measure the saved file size
            string fullPath = fileStorageService.GetFullPath(filePath);
            fileSize = new FileInfo(fullPath).Length;
        }
        else
        {
            using var stream = file.OpenReadStream();
            filePath = await fileStorageService.SaveFileAsync(stream, Constants.FileStorage.Documents, file.FileName);
        }

        var tagList = string.IsNullOrWhiteSpace(tags) ? [] : tags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

        var result = await documentService.CreateAsync(title, description, filePath, contentType, fileSize, personResult.Value.Id, tagList);
        return result.IsSuccess ? Ok(result.Value) : BadRequest();
    }
}