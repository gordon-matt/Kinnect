namespace Kinnect.Controllers;

[Authorize]
public class FamilyTreeController(
    IPersonService personService,
    IUserContextService userContextService) : Controller
{
    public async Task<IActionResult> Index()
    {
        bool isAdmin = User.IsInRole(Constants.Roles.Administrator);
        ViewData["IsAdmin"] = isAdmin;

        int? myPersonId = null;

        string? userId = userContextService.GetCurrentUserId();
        if (userId != null)
        {
            var result = await personService.GetByUserIdAsync(userId);
            if (result.IsSuccess)
            {
                myPersonId = result.Value.Id;
            }
        }

        ViewData["MyPersonId"] = myPersonId;

        return View();
    }
}