using System.Security.Claims;

namespace Kinnect.Controllers;

[Authorize(Roles = Constants.Roles.Administrator)]
public class AdminController : Controller
{
    public IActionResult Users()
    {
        ViewData["CurrentUserId"] = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return View();
    }

    public IActionResult PersonBackups() => View();
}