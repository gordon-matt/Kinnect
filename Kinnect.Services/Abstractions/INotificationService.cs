namespace Kinnect.Services.Abstractions;

public interface INotificationService
{
    /// <summary>Creates a persisted unread notification for a private message recipient.</summary>
    Task CreateAsync(int chatMessageId, string fromUserId, string toUserId);

    /// <summary>Returns unread notification counts grouped by sender for a given recipient.</summary>
    Task<IEnumerable<UnreadNotificationDto>> GetUnreadSummaryAsync(string toUserId);

    /// <summary>Marks all notifications from a specific sender as read.</summary>
    Task MarkReadAsync(string toUserId, string fromUserId);
}
