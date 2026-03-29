namespace Kinnect.Services;

public class NotificationService(
    IRepository<MessageNotification> notificationRepository,
    IUserInfoService userInfoService) : INotificationService
{
    public async Task CreateAsync(int chatMessageId, string fromUserId, string toUserId)
    {
        await notificationRepository.InsertAsync(new MessageNotification
        {
            ChatMessageId = chatMessageId,
            FromUserId = fromUserId,
            ToUserId = toUserId,
            IsRead = false,
            EmailSent = false,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    public async Task<IEnumerable<UnreadNotificationDto>> GetUnreadSummaryAsync(string toUserId)
    {
        var notifications = await notificationRepository.FindAsync(new SearchOptions<MessageNotification>
        {
            Query = n => n.ToUserId == toUserId && !n.IsRead
        });

        var grouped = notifications
            .GroupBy(n => n.FromUserId)
            .Select(g => new { FromUserId = g.Key, Count = g.Count() })
            .ToList();

        if (grouped.Count == 0)
            return [];

        var fromUserIds = grouped.Select(g => g.FromUserId).ToList();
        var userInfo = await userInfoService.GetUserInfoAsync(fromUserIds);

        return grouped.Select(g => new UnreadNotificationDto
        {
            FromUserId = g.FromUserId,
            FromDisplayName = userInfo.GetValueOrDefault(g.FromUserId)?.Username ?? g.FromUserId,
            UnreadCount = g.Count
        });
    }

    public async Task MarkReadAsync(string toUserId, string fromUserId)
    {
        var notifications = await notificationRepository.FindAsync(new SearchOptions<MessageNotification>
        {
            Query = n => n.ToUserId == toUserId && n.FromUserId == fromUserId && !n.IsRead
        });

        foreach (var n in notifications)
        {
            n.IsRead = true;
            await notificationRepository.UpdateAsync(n);
        }
    }
}
