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
        return View("ViewProfile");
    }
}