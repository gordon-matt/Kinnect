using System.Diagnostics;
using Kinnect.Data;

namespace Kinnect.Controllers;

[Authorize]
public class HomeController(
    IUserContextService userContextService,
    ApplicationDbContext context) : Controller
{
    private static bool hasSeeded = false;

    public async Task<IActionResult> Index()
    {
        if (!hasSeeded && Constants.UseKeyCloak)
        {
            string currentUserId = userContextService.GetCurrentUserId()!;
            await DbInitializer.SeedAnnouncementsRoomAsync(context, currentUserId);

            hasSeeded = true;
        }

        bool hasLinkedPerson = await context.People.AnyAsync();
        return !hasLinkedPerson ? RedirectToAction("Index", "Onboarding") : View();
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}