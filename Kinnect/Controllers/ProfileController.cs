using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers;

[Authorize]
public class ProfileController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult View(int id)
    {
        ViewData["PersonId"] = id;
        ViewData["IsAdmin"] = User.IsInRole(Constants.Roles.Administrator);
        return View("ViewProfile");
    }
}
