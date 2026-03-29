using System.Linq;
using System.Security.Claims;
using Kinnect.Infrastructure;
using Kinnect.Models.Requests.Admin;
using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Constants.Roles.Administrator)]
public class AdminApiController(
    IPersonService personService,
    IPersonBackupService personBackupService,
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
            var roles = await userManager.GetRolesAsync(user);

            result.Add(new
            {
                userId = user.Id,
                email = user.Email,
                userName = user.UserName,
                isPendingApproval = isLocked,
                personId = person?.Id,
                personName = person?.FullName,
                role = roles.FirstOrDefault() ?? string.Empty
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

    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> ChangeUserRole(string userId, [FromBody] ChangeUserRoleRequest request)
    {
        var userManager = GetUserManager();
        if (userManager is null)
        {
            return BadRequest("User management is only available when using ASP.NET Identity.");
        }

        string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == currentUserId)
        {
            return BadRequest("You cannot change your own role.");
        }

        string[] validRoles = [Constants.Roles.Administrator, Constants.Roles.Editor, Constants.Roles.User];
        if (!validRoles.Contains(request.Role))
        {
            return BadRequest($"Invalid role. Must be one of: {string.Join(", ", validRoles)}.");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRoleAsync(user, request.Role);

        return Ok(new { role = request.Role });
    }

    [HttpGet("person-backups")]
    public async Task<IActionResult> GetPersonBackups(CancellationToken cancellationToken)
    {
        var result = await personBackupService.ListBackupsAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(string.Join("; ", result.Errors));
        }

        return Ok(result.Value);
    }

    [HttpPost("person-backups/restore")]
    public async Task<IActionResult> RestorePersonBackup(
        [FromBody] PersonBackupRestoreRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.FileName))
        {
            return BadRequest("File name is required.");
        }

        var result = await personBackupService.RestoreFromFileAsync(request.FileName, cancellationToken);
        if (!result.IsSuccess)
        {
            string message = result.ValidationErrors.Any()
                ? string.Join("; ", result.ValidationErrors.Select(e => e.ErrorMessage))
                : string.Join("; ", result.Errors);
            return BadRequest(message);
        }

        return Ok(new { message = "Person tree restored from backup." });
    }
}