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
}