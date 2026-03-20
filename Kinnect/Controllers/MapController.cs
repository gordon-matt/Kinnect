using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers;

[Authorize]
public class MapController : Controller
{
    public IActionResult Index()
    {
        ViewData["GoogleMapsApiKey"] = HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetValue<string>("GoogleMaps:ApiKey") ?? "";
        return View();
    }
}
