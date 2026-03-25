namespace Kinnect.Controllers;

[Authorize]
public class ProfileController(IPersonService personService, IUserContextService userContextService) : Controller
{
    public IActionResult Index() => View();

    public async Task<IActionResult> Edit(int id)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var personResult = await personService.GetByIdAsync(id);
        if (!personResult.IsSuccess)
        {
            return NotFound();
        }

        var person = personResult.Value;
        bool isAdmin = User.IsInRole(Constants.Roles.Administrator);
        bool canEdit = isAdmin || person.UserId == null || person.UserId == userId;
        if (!canEdit)
        {
            return Forbid();
        }

        ViewData["EditPersonId"] = id;
        ViewData["Title"] = "Edit profile";
        return View("Index");
    }

    public IActionResult View(int id)
    {
        ViewData["PersonId"] = id;
        ViewData["IsAdmin"] = User.IsInRole(Constants.Roles.Administrator);
        return View("ViewProfile");
    }

    public async Task<IActionResult> EventMap(int id)
    {
        var personResult = await personService.GetByIdAsync(id);
        if (!personResult.IsSuccess)
        {
            return NotFound();
        }

        ViewData["PersonId"] = id;
        ViewData["PersonName"] = personResult.Value.FullName;
        ViewData["Title"] = $"{personResult.Value.FullName} — Event Map";
        return View("EventMap");
    }

    public async Task<IActionResult> PhotoMap(int id)
    {
        var personResult = await personService.GetByIdAsync(id);
        if (!personResult.IsSuccess)
        {
            return NotFound();
        }

        ViewData["PersonId"] = id;
        ViewData["PersonName"] = personResult.Value.FullName;
        ViewData["Title"] = $"{personResult.Value.FullName} — Photo Map";
        return View("PhotoMap");
    }
}