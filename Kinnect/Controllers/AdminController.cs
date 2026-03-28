namespace Kinnect.Controllers;

[Authorize(Roles = Constants.Roles.Administrator)]
public class AdminController : Controller
{
    public IActionResult Users() => View();

    public IActionResult PersonBackups() => View();
}