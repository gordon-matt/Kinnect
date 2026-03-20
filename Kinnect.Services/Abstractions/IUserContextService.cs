namespace Kinnect.Services.Abstractions;

public interface IUserContextService
{
    string? GetCurrentUserId();
    bool IsAuthenticated();
    bool IsAdmin();
}
