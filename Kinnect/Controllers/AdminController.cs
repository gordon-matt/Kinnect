using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers;

[Authorize(Roles = Constants.Roles.Administrator)]
public class AdminController : Controller
{
    public IActionResult Users()
    {
        return View();
    }
}
