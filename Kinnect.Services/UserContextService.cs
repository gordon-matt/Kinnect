using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Kinnect.Services;

public class UserContextService(IHttpContextAccessor httpContextAccessor) : IUserContextService
{
    public string? GetCurrentUserId() => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsAdmin() => httpContextAccessor.HttpContext?.User?.IsInRole(Constants.Roles.Administrator) == true;

    public bool IsEditor() =>
        IsAdmin() ||
        httpContextAccessor.HttpContext?.User?.IsInRole(Constants.Roles.Editor) == true;

    public bool IsAuthenticated() => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}