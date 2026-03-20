using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers;

[Authorize]
public class FamilyTreeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
