namespace Kinnect.Models.Dto;

public record UnreadNotificationDto
{
    public string FromUserId { get; init; } = null!;

    public string FromDisplayName { get; init; } = null!;

    public int UnreadCount { get; init; }
}
