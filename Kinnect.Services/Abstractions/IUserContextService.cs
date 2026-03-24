namespace Kinnect.Services.Abstractions;

public interface IUserContextService
{
    string? GetCurrentUserId();

    bool IsAdmin();

    bool IsAuthenticated();
}