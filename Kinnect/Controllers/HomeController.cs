using System.Diagnostics;
using Kinnect.Data;

namespace Kinnect.Controllers;

[Authorize]
public class HomeController(
    IUserContextService userContextService,
    IUserInfoService userInfoService,
    ApplicationDbContext context) : Controller
{
    private static bool hasSeeded = false;

    public async Task<IActionResult> Index()
    {
        if (!hasSeeded && Constants.UseKeyCloak)
        {
            string userId = userContextService.GetCurrentUserId()!;

            if (!await context.People.AnyAsync())
            {
                var userInfo = await userInfoService.GetUserInfoAsync([userId]);
                string[] name = userInfo.GetValueOrDefault(userId)?.Username?.Split(' ') ?? ["Admin", "User"];
                string familyName = name.Length > 1 ? name[^1] : "Admin";
                string givenNames = name.Length > 1 ? string.Join(' ', name[..^1]) : name.First();
                await DbInitializer.SeedInitialPersonAsync(context, userId, familyName, givenNames);
            }

            await DbInitializer.SeedAnnouncementsRoomAsync(context, userId);
            hasSeeded = true;
        }

        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}