using Kinnect.Data.Entities;
using Kinnect.Infrastructure;
using Kinnect.Services;
using Microsoft.AspNetCore.Identity;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Constants.Roles.Administrator)]
public class AdminApiController(
    IPersonService personService,
    IAuthProviderService authProviderService,
    IServiceProvider serviceProvider) : ControllerBase
{
    private UserManager<ApplicationUser>? GetUserManager() =>
        authProviderService.IsIdentity
            ? serviceProvider.GetService<UserManager<ApplicationUser>>()
            : null;

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var userManager = GetUserManager();
        if (userManager is null)
        {
            return BadRequest("User management is only available when using ASP.NET Identity.");
        }

        var users = userManager.Users.ToList();

        var personsResult = await personService.GetAllAsync();
        var personsByUserId = personsResult.IsSuccess
            ? personsResult.Value
                .Where(p => p.UserId != null)
                .ToDictionary(p => p.UserId!)
            : [];

        var result = new List<object>();
        foreach (var user in users.OrderBy(u => u.Email))
        {
            bool isLocked = user.LockoutEnabled
                && user.LockoutEnd.HasValue
                && user.LockoutEnd > DateTimeOffset.UtcNow;

            personsByUserId.TryGetValue(user.Id, out var person);

            result.Add(new
            {
                userId = user.Id,
                email = user.Email,
                userName = user.UserName,
                isPendingApproval = isLocked,
                personId = person?.Id,
                personName = person?.FullName
            });
        }

        return Ok(result);
    }

    [HttpPost("users/{userId}/approve")]
    public async Task<IActionResult> ApproveUser(string userId)
    {
        var userManager = GetUserManager();
        if (userManager is null)
        {
            return BadRequest("User management is only available when using ASP.NET Identity.");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        await userManager.SetLockoutEndDateAsync(user, null);

        return Ok(new { message = "User approved successfully." });
    }

    [HttpPost("users/{userId}/lock")]
    public async Task<IActionResult> LockUser(string userId)
    {
        var userManager = GetUserManager();
        if (userManager is null)
        {
            return BadRequest("User management is only available when using ASP.NET Identity.");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        return Ok(new { message = "User locked successfully." });
    }
}
