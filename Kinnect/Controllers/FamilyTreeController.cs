using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kinnect.Controllers;

[Authorize]
public class FamilyTreeController(IPersonService personService, IUserContextService userContextService) : Controller
{
    public async Task<IActionResult> Index()
    {
        bool isAdmin = User.IsInRole(Constants.Roles.Administrator);
        ViewData["IsAdmin"] = isAdmin;

        string? userId = userContextService.GetCurrentUserId();
        int? myPersonId = null;

        if (userId != null)
        {
            var result = await personService.GetByUserIdAsync(userId);
            if (result.IsSuccess)
                myPersonId = result.Value.Id;
        }

        ViewData["MyPersonId"] = myPersonId;

        return View();
    }
}
