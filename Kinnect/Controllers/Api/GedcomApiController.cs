using System.Text;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/gedcom")]
[Authorize]
public class GedcomApiController(IGedcomService gedcomService) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        string gedcom = await gedcomService.ExportAsync();
        string fileName = $"kinnect-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.ged";
        return File(Encoding.UTF8.GetBytes(gedcom), "text/plain", fileName);
    }

    [HttpPost("import")]
    [Authorize(Roles = Constants.Roles.Administrator)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "Please upload a .ged file." });
        }

        using var stream = file.OpenReadStream();
        var result = await gedcomService.ImportAsync(stream);

        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = string.Join("; ", result.Errors) });
    }
}