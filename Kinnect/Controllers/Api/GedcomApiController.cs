using Kinnect.Data;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/gedcom")]
[Authorize]
public class GedcomApiController(
    IGedcomService gedcomService,
    IUserContextService users,
    ApplicationDbContextBase database) : ControllerBase
{
    [HttpPost("import")]
    [RequestSizeLimit(Constants.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = Constants.MaxUploadBytes)]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (users.GetCurrentUserId() is null)
        {
            return Unauthorized();
        }

        if (!users.IsAdmin())
        {
            return Fail("Only an administrator can import a GEDCOM.");
        }

        if (await database.People.AnyAsync())
        {
            return Fail("GEDCOM import is only available once during first-time setup.");
        }

        if (file is null || file.Length == 0)
        {
            return Fail("Please select a GEDCOM file to import.");
        }

        await using var stream = file.OpenReadStream();
        var result = await gedcomService.ImportAsync(stream, file.FileName);
        return result.IsSuccess
            ? Ok(result.Value)
            : Fail(string.Join("; ", result.Errors));
    }

    private static ContentResult Fail(string message) => new()
    {
        StatusCode = StatusCodes.Status400BadRequest,
        Content = message,
        ContentType = "text/plain"
    };
}
