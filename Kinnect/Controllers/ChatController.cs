using Kinnect.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kinnect.Controllers;

[Authorize]
public class ChatController(ApplicationDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index([FromQuery] string? withUser)
    {
        ViewData["Title"] = "Messages";

        if (!string.IsNullOrEmpty(withUser))
        {
            var targetUser = await dbContext.Users.FindAsync(withUser);
            if (targetUser is not null)
            {
                var person = await dbContext.People
                    .Where(p => p.UserId == withUser)
                    .Select(p => new { p.GivenNames, p.FamilyName })
                    .FirstOrDefaultAsync();

                ViewData["InitialPrivateUserId"] = withUser;
                ViewData["InitialPrivateUserName"] = person is not null
                    ? $"{person.GivenNames} {person.FamilyName}".Trim()
                    : targetUser.UserName;
            }
        }

        return View();
    }
}
