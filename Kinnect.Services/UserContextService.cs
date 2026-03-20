using System.Security.Claims;
using Kinnect.Services.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Kinnect.Services;

public class UserContextService(IHttpContextAccessor httpContextAccessor) : IUserContextService
{
    public string? GetCurrentUserId()
    {
        return httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public bool IsAuthenticated()
    {
        return httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
    }

    public bool IsAdmin()
    {
        return httpContextAccessor.HttpContext?.User?.IsInRole(Constants.Roles.Administrator) == true;
    }
}
