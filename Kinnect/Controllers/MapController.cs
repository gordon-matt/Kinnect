namespace Kinnect.Controllers;

[Authorize]
public class MapController : Controller
{
    public IActionResult Index() => View();
}